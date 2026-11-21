using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Matching;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Writing;

public static class JobRewriteWorker
{
    public static async Task Process(IServiceProvider sp, AiJob job, string owner, long generation, CancellationToken stoppingToken)
    {
        var db = sp.GetRequiredService<AppDb>();
        var service = sp.GetRequiredService<JobService>();
        Guid? attempt = null;
        LlmReply? reply = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(70));
        try
        {
            var input = await db.JobRewriteInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == job.Id, timeout.Token);
            if (input is null || !await JobRewriteService.CanAccess(db, job.RequestedBy, input, timeout.Token))
                throw new ProviderError("access_revoked", false);
            var method = await db.AiMethods.AsNoTracking().SingleAsync(x => x.Id == job.MethodVersionId, timeout.Token);
            var parameters = JsonSerializer.Deserialize<MethodParameters>(method.Parameters, OutputValidator.Json)!;
            if (parameters.Contract != JobRewriteContract.Version)
                throw new ProviderError("writing_method_unavailable", false);
            var payload = JsonSerializer.Deserialize<JobRewritePayload>(input.Payload, OutputValidator.Json)!;
            attempt = await service.StartAttempt(job.Id, owner, generation, timeout.Token);
            if (attempt is null) return;
            reply = method.Provider == "Fake"
                ? await sp.GetRequiredService<FakeLlmClient>().RewriteJobAsync(payload, parameters, timeout.Token)
                : await sp.GetRequiredService<DeepSeekClient>().CompleteAsync(input.Payload, JobRewriteContract.Prompt, parameters, timeout.Token);
            CompletionValidator.Validate(reply);
            var sections = JobRewriteContract.Validate(reply.Json, payload);
            var serialized = PairMatchingContract.Serialize(sections);
            await service.Settle(attempt.Value, reply, true, stoppingToken);
            await service.Finish(job.Id, owner, generation, serialized, null, false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            db.ChangeTracker.Clear();
            if (attempt is { } id) await service.Settle(id, reply, false, stoppingToken);
            await service.Finish(job.Id, owner, generation, null,
                e is ProviderError error ? error.Code : e is OperationCanceledException ? "provider_timeout" : "processing_failed",
                e is HttpRequestException or OperationCanceledException || e is ProviderError { Retryable: true }, stoppingToken);
        }
    }
}
