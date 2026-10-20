using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class AiDispatchTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Theory]
    [InlineData("skills-v1")]
    [InlineData("cv-fields-v3")]
    public async Task DisabledAiStopsAdmissionAndDispatchWhileDurableJobSurvivesHostReplacement(string contract)
    {
        var (candidate, userId) = await fixture.User();
        var file = await FoundationFixture.Upload(candidate);
        var versionId = file.GetProperty("versionId").GetGuid();
        using var admission = fixture.App.Services.CreateScope();
        var queued = await admission.ServiceProvider.GetRequiredService<JobService>().Enqueue(userId, new(versionId, Guid.NewGuid(), contract), default);
        var storage = Path.Combine(Path.GetTempPath(), new Npgsql.NpgsqlConnectionStringBuilder(fixture.Connection).Database!);
        long generation;
        const string owner = "synthetic-dispatch-check";
        using (var disabled = new TestApp(fixture.Connection, storage))
        {
            using var scope = disabled.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<IOptions<AiOptions>>().Value.Enabled = false;
            var service = scope.ServiceProvider.GetRequiredService<JobService>();
            var rejected = await Assert.ThrowsAsync<JobError>(() => service.Enqueue(userId, new(versionId, Guid.NewGuid(), contract), default));
            Assert.Equal("ai_disabled", rejected.Code);
            Assert.Equal(503, rejected.Status);
            var claimed = await service.Claim(owner, default);
            Assert.NotNull(claimed);
            Assert.Equal(queued.Id, claimed.Id);
            generation = claimed.LeaseGeneration;
            var blocked = await Assert.ThrowsAsync<ProviderError>(() => service.StartAttempt(queued.Id, owner, generation, default));
            Assert.Equal("ai_disabled", blocked.Code);
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal(1, await db.AiJobs.CountAsync(x => x.Id == queued.Id));
            Assert.Empty(await db.ExternalCallAttempts.Where(x => x.AiJobId == queued.Id).ToListAsync());
            Assert.Equal(2m, (await db.BudgetReservations.SingleAsync(x => x.AiJobId == queued.Id)).ReservedRemaining);
        }
        // A replacement host consumes the same persisted job/lease; no re-enqueue/key.
        using var replacement = new TestApp(fixture.Connection, storage);
        using var replacementScope = replacement.Services.CreateScope();
        await JobWorker.Process(replacementScope.ServiceProvider, queued.Id, owner, generation, default);
        var restoredDb = replacementScope.ServiceProvider.GetRequiredService<AppDb>();
        restoredDb.ChangeTracker.Clear();
        Assert.Equal("succeeded", (await restoredDb.AiJobs.SingleAsync(x => x.Id == queued.Id)).Status);
        Assert.Single(await restoredDb.ExternalCallAttempts.Where(x => x.AiJobId == queued.Id).ToListAsync());
        Assert.Equal(0m, (await restoredDb.BudgetReservations.SingleAsync(x => x.AiJobId == queued.Id)).ReservedRemaining);
    }
}
