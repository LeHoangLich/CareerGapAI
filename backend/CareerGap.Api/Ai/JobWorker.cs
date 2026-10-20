using System.Text.Json;
using System.Security.Cryptography;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareerGap.Api.Ai;

public sealed class JobWakeup
{
    private readonly SemaphoreSlim signal = new(0, 1);
    public void Signal() { if (signal.CurrentCount == 0) try { signal.Release(); } catch (SemaphoreFullException) { } }
    public Task<bool> Wait(TimeSpan timeout, CancellationToken ct) => signal.WaitAsync(timeout, ct);
}
public sealed class JobWorker(IServiceScopeFactory scopes, JobWakeup wakeup, ILogger<JobWorker> logger, IOptions<AiOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var owner = Guid.NewGuid().ToString("N");
        var idle = 1;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var service = scope.ServiceProvider.GetRequiredService<JobService>();
                var job = await service.Claim(owner, stoppingToken);
                if (job is null)
                {
                    await wakeup.Wait(TimeSpan.FromSeconds(Math.Min(60, Math.Max(5, options.Value.PollSeconds) * idle)), stoppingToken);
                    idle = Math.Min(4, idle + 1); continue;
                }
                idle = 1;
                await Process(scope.ServiceProvider, job.Id, owner, job.LeaseGeneration, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Avoid exception text: drivers/providers may include credentials or private document fragments.
                logger.LogWarning("Worker unavailable ({ExceptionType}); durable jobs will be checked again", ex.GetType().Name);
                await wakeup.Wait(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
    public static async Task Process(IServiceProvider sp, Guid jobId, string owner, long generation, CancellationToken stoppingToken)
    {
        var db = sp.GetRequiredService<AppDb>();
        db.ChangeTracker.Clear();
        var service = sp.GetRequiredService<JobService>();
        var job = await db.AiJobs.AsNoTracking().SingleAsync(x => x.Id == jobId, stoppingToken);
        var method = await db.AiMethods.AsNoTracking().SingleAsync(x => x.Id == job.MethodVersionId, stoppingToken);
        var parameters = JsonSerializer.Deserialize<MethodParameters>(method.Parameters, OutputValidator.Json)!;
        Guid? attemptId = null;
        LlmReply? reply = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(70));
        try
        {
            var version = await db.ResumeVersions.AsNoTracking().SingleAsync(x => x.Id == job.ResumeVersionId && x.CandidateId == job.RequestedBy, timeout.Token);
            var file = await db.StoredFiles.AsNoTracking().SingleAsync(x => x.Id == version.FileId && x.OwnerUserId == job.RequestedBy && x.Status == "ready", timeout.Token);
            if (!await db.Users.AnyAsync(x => x.Id == job.RequestedBy && x.AccountStatus == "active", timeout.Token)) throw new ProviderError("access_revoked", false);
            var storage = sp.GetRequiredService<IFileStorage>();
            if (storage.Provider != file.Provider) throw new ProviderError("storage_unavailable", false);
            var bytes = await storage.ReadAsync(file.ObjectKey, timeout.Token);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != version.DocumentHash) throw new ProviderError("file_unavailable", false);
            var doc = sp.GetRequiredService<DocumentParser>().Parse(bytes, file.OriginalName, timeout.Token);
            if (method.Provider == "DeepSeek" && System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(doc.Segments, OutputValidator.Json) + (parameters.Contract == FullCvExtraction.Contract ? FullCvExtraction.Prompt : DeepSeekClient.Prompt)) + 1024 > parameters.MaxInputTokens) throw new ProviderError("ai_input_limit", false);
            attemptId = await service.StartAttempt(jobId, owner, generation, timeout.Token);
            if (attemptId is null) return;
            ILlmClient client = method.Provider == "Fake" ? sp.GetRequiredService<FakeLlmClient>() : sp.GetRequiredService<DeepSeekClient>();
            reply = await client.ExtractAsync(doc, parameters, timeout.Token);
            CompletionValidator.Validate(reply);
            var full = parameters.Contract == FullCvExtraction.Contract ? FullCvExtraction.Validate(reply.Json, doc) : null;
            var output = full is null ? OutputValidator.Validate(reply.Json, doc) : null;
            await service.Settle(attemptId.Value, reply, true, stoppingToken);
            await service.Finish(jobId, owner, generation, full is null ? JsonSerializer.Serialize(output, OutputValidator.Json) : reply.Json, null,
                false, stoppingToken, doc, output, full);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { /* lease expires; a restart reclaims and accounts for the unknown call */ }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            db.ChangeTracker.Clear();
            if (attemptId is not null) await service.Settle(attemptId.Value, reply, false, stoppingToken);
            var code = ex switch { ProviderError p => p.Code, DocumentError d => d.Code, OperationCanceledException => "provider_timeout", _ => "processing_failed" };
            var retry = ex is HttpRequestException or OperationCanceledException || ex is ProviderError { Retryable: true };
            await service.Finish(jobId, owner, generation, null, code, retry, stoppingToken);
        }
    }
}
