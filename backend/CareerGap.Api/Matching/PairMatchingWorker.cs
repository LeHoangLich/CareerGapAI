using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Matching;

public static class PairMatchingWorker
{
    public static async Task Process(IServiceProvider sp, AiJob job, string owner, long generation, CancellationToken stoppingToken)
    {
        var db = sp.GetRequiredService<AppDb>();
        var service = sp.GetRequiredService<JobService>();
        Guid? attempt = null; LlmReply? reply = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(70));
        try
        {
            var input = await db.MatchInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == job.Id, timeout.Token);
            if (input is null || !await PairMatchingService.CanAccess(db, job.RequestedBy, input, DateTimeOffset.UtcNow, timeout.Token)) throw new ProviderError("access_revoked", false);
            var method = await db.AiMethods.AsNoTracking().SingleAsync(x => x.Id == job.MethodVersionId, timeout.Token);
            var parameters = JsonSerializer.Deserialize<MethodParameters>(method.Parameters, OutputValidator.Json)!;
            if (parameters.Contract != PairMatchingContract.Version) throw new ProviderError("match_method_unavailable", false);
            var payload = JsonSerializer.Deserialize<PairPayload>(input.Payload, OutputValidator.Json)!;
            attempt = await service.StartAttempt(job.Id, owner, generation, timeout.Token);
            if (attempt is null) return;
            reply = method.Provider == "Fake"
                ? await sp.GetRequiredService<FakeLlmClient>().MatchAsync(payload, parameters, timeout.Token)
                : await sp.GetRequiredService<DeepSeekClient>().CompleteAsync(input.Payload, PairMatchingContract.Prompt, parameters, timeout.Token);
            CompletionValidator.Validate(reply);
            PairMatchingContract.Validate(reply.Json, payload);
            await service.Settle(attempt.Value, reply, true, stoppingToken);
            await service.Finish(job.Id, owner, generation, reply.Json, null, false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            db.ChangeTracker.Clear();
            if (attempt.HasValue) await service.Settle(attempt.Value, reply, false, stoppingToken);
            var code = ex switch { ProviderError p => p.Code, OperationCanceledException => "provider_timeout", _ => "processing_failed" };
            await service.Finish(job.Id, owner, generation, null, code,
                ex is HttpRequestException or OperationCanceledException || ex is ProviderError { Retryable: true }, stoppingToken);
        }
    }
}
