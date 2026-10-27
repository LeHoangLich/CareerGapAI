using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Matching;
using CareerGap.Api.Writing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class WritingContractTests
{
    private static WritingPayload Payload => new("vi", [new("skills[0]", "React", "document_span"),
        new("experience[0].description", "Built 2 React dashboards.", "user_statement"),
        new("experience[1].description", "Led a different synthetic project.", "document_span")], ["summary", "experience[0].description"]);
    [Fact] public void ComposesOnlyWholeFactsAndRetainsStatementAttribution()
    {
        var output = WritingContract.Validate(PairMatchingContract.Serialize(WritingContract.Fake(Payload)), Payload);
        Assert.Equal(2, output.Count); Assert.Contains("Kỹ năng: React", output[0].Text);
        Assert.Equal("Built 2 React dashboards.", output[1].Text);
        Assert.Equal("user_statement", Assert.Single(output[1].Sources).SourceType);
        var english = WritingContract.Validate(PairMatchingContract.Serialize(WritingContract.Fake(Payload)), Payload with { Language = "en" });
        Assert.Contains("Skill: React", english[0].Text);
    }
    [Theory]
    [InlineData("{\"sections\":[],\"text\":\"Invented 20 years experience\"}")]
    [InlineData("{\"sections\":[{\"target\":\"summary\",\"factKeys\":[\"invented\"]},{\"target\":\"experience[0].description\",\"factKeys\":[\"experience[0].description\"]}]}")]
    [InlineData("{\"sections\":[{\"target\":\"summary\",\"factKeys\":[\"skills[0]\"]},{\"target\":\"experience[0].description\",\"factKeys\":[\"experience[1].description\"]}]}")]
    [InlineData("{\"sections\":[{\"target\":\"summary\",\"factKeys\":[\"skills[0]\",\"skills[0]\"]},{\"target\":\"summary\",\"factKeys\":[\"skills[0]\"]}]}")]
    public void RejectsNewFactsFreeProseWrongGroupAndDuplicateTargets(string raw) =>
        Assert.Equal("writing_output_invalid", Assert.Throws<ProviderError>(() => WritingContract.Validate(raw, Payload)).Code);
}

public sealed class WritingTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private const string Password = "SyntheticWriting!Password-2026";
    private async Task<(HttpClient Client, Guid User, Guid Version)> Prepare()
    {
        var (client, uid) = await fixture.User(password: Password);
        var upload = await FoundationFixture.Upload(client, FoundationFixture.Docx()); var version = upload.GetProperty("versionId").GetGuid();
        var content = new ResumeBuilderContentRequest(0, "simple-v1", "Synthetic Writer", null, null, null, "Developer", "Built React tools.", ["React", "TypeScript"],
            [new("Developer", "Synthetic Lab", "2024", "Built 2 React dashboards.")], [], [new("Synthetic Portal", "Practice", "Created a TypeScript portal.")], []);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/resume-versions/{version}/revisions/manual", content)).StatusCode);
        using var scope = fixture.App.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDb>().ResumeDataRevisions.AsNoTracking().SingleAsync(x => x.ResumeVersionId == version);
        Assert.True(ResumeSkillProjection.TryRead(stored, false, out var skills)); Assert.Contains("React", skills);
        return (client, uid, version);
    }
    private async Task<Guid> Enqueue(HttpClient client, Guid version, Guid? key = null)
    {
        var response = await client.PostAsJsonAsync("/api/writing/proposals", new CreateWritingRequest(version, key ?? Guid.NewGuid()));
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private async Task Drain(Guid id)
    {
        using var scope = fixture.App.Services.CreateScope(); var service = scope.ServiceProvider.GetRequiredService<JobService>();
        var job = await service.Claim("writing-test", default); Assert.Equal(id, job!.Id);
        await JobWorker.Process(scope.ServiceProvider, id, "writing-test", job.LeaseGeneration, default);
    }
    [Fact] public async Task ProposalIsOwnerOnlyRequiresConsentAndNeverCreatesOrOverwritesCv()
    {
        var s = await Prepare(); var key = Guid.NewGuid(); var id = await Enqueue(s.Client, s.Version, key); await Drain(id);
        Assert.Equal(id, await Enqueue(s.Client, s.Version, key));
        var view = await s.Client.GetFromJsonAsync<JsonElement>($"/api/writing/proposals/{id}");
        Assert.Equal("succeeded", view.GetProperty("status").GetString()); Assert.Equal(JsonValueKind.Null, view.GetProperty("decision").ValueKind);
        var recovered = await s.Client.GetFromJsonAsync<JsonElement>($"/api/writing/proposals?resumeVersionId={s.Version}");
        Assert.Equal(id, recovered.GetProperty("proposal").GetProperty("id").GetGuid());
        var (other, _) = await fixture.User(); var (admin, _) = await fixture.User("Admin"); var (recruiter, _) = await fixture.User("Recruiter");
        foreach (var caller in new[] { other, admin, recruiter })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await caller.GetAsync($"/api/writing/proposals/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await caller.GetAsync($"/api/ai/jobs/{id}")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/writing/proposals?resumeVersionId={s.Version}")).StatusCode);
        var decision = new WritingDecisionRequest("accept", [new("summary", "I edited this as my own statement.")]);
        Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsJsonAsync($"/api/writing/proposals/{id}/decision", decision)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsJsonAsync($"/api/writing/proposals/{id}/decision", decision)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Client.PostAsJsonAsync($"/api/writing/proposals/{id}/decision", new WritingDecisionRequest("discard"))).StatusCode);
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Single(await db.ResumeVersions.Where(x => x.CandidateId == s.User).ToArrayAsync());
        Assert.Single(await db.ResumeDataRevisions.Where(x => x.CandidateId == s.User).ToArrayAsync());
        var input = await db.WritingInputs.SingleAsync(x => x.AiJobId == id);
        Assert.DoesNotContain("Synthetic Writer", input.Payload); Assert.DoesNotContain("displayName", input.Payload);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE writing_results SET sections='[]'::jsonb WHERE ai_job_id={id}"));
    }
    [Fact] public async Task DiscardDoesNotSaveAnyCvAndWrongVersionIsDenied()
    {
        var s = await Prepare(); var id = await Enqueue(s.Client, s.Version);
        long abandonedGeneration;
        using (var abandoned = fixture.App.Services.CreateScope())
        {
            var claimed = await abandoned.ServiceProvider.GetRequiredService<JobService>().Claim("abandoned-writer", default);
            Assert.NotNull(claimed); Assert.Equal(id, claimed.Id); abandonedGeneration = claimed.LeaseGeneration;
            await abandoned.ServiceProvider.GetRequiredService<AppDb>().Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_jobs SET lease_until = now() - interval '1 second' WHERE id = {id}");
        }
        {
            using var recovered = fixture.App.Services.CreateScope(); var service = recovered.ServiceProvider.GetRequiredService<JobService>();
            var claimed = await service.Claim("recovered-writer", default); Assert.NotNull(claimed); Assert.Equal(id, claimed.Id);
            Assert.True(claimed.LeaseGeneration > abandonedGeneration);
            Assert.False(await service.Finish(id, "abandoned-writer", abandonedGeneration, "{}", null, false, default));
            Assert.False(await recovered.ServiceProvider.GetRequiredService<AppDb>().WritingResults.AnyAsync(x => x.AiJobId == id));
            await JobWorker.Process(recovered.ServiceProvider, id, "recovered-writer", claimed.LeaseGeneration, default);
            var persisted = await recovered.ServiceProvider.GetRequiredService<AppDb>().AiJobs.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.Equal("succeeded", persisted.Status); Assert.Equal(2, persisted.AttemptCount);
            Assert.Equal(1, await recovered.ServiceProvider.GetRequiredService<AppDb>().ExternalCallAttempts.CountAsync(x => x.AiJobId == id));
        }
        var (other, _) = await fixture.User();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync("/api/writing/proposals", new CreateWritingRequest(s.Version, Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsJsonAsync($"/api/writing/proposals/{id}/decision", new WritingDecisionRequest("discard"))).StatusCode);
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal("discard", (await db.WritingDecisions.SingleAsync(x => x.AiJobId == id)).Action);
        Assert.Single(await db.ResumeVersions.Where(x => x.CandidateId == s.User).ToArrayAsync());
    }
    [Fact] public async Task AccountPurgeErasesProposalsAndDecisionsButPreservesAttemptUsage()
    {
        var s = await Prepare(); var id = await Enqueue(s.Client, s.Version); await Drain(id);
        Assert.Equal(HttpStatusCode.OK, (await s.Client.PostAsJsonAsync($"/api/writing/proposals/{id}/decision", new WritingDecisionRequest("discard"))).StatusCode);
        Guid reportId;
        using (var seedScope = fixture.App.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDb>();
            var report = new ContentReport { ReporterId = s.User, TargetType = "public_resume", TargetId = s.Version, Reason = "Synthetic subject details" };
            reportId = report.Id; seedDb.ContentReports.Add(report); seedDb.AdminAudits.Add(new() { ActorId = Guid.NewGuid(), TargetId = report.Id, Action = "report_resolved", Reason = "Synthetic moderator details" }); await seedDb.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Accepted, (await s.Client.PostAsJsonAsync("/api/account/deletion-request", new { password = Password })).StatusCode);
        fixture.Clock.Advance(TimeSpan.FromDays(8));
        try
        {
            using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.True(await scope.ServiceProvider.GetRequiredService<CareerGap.Api.Accounts.AccountDeletionService>().ProcessOneDueAsync("writing-purge", default));
            Assert.Equal("deleted", (await db.Users.AsNoTracking().SingleAsync(x => x.Id == s.User)).AccountStatus);
            Assert.False(await db.WritingInputs.AnyAsync(x => x.AiJobId == id)); Assert.False(await db.WritingResults.AnyAsync(x => x.AiJobId == id)); Assert.False(await db.WritingDecisions.AnyAsync(x => x.AiJobId == id));
            Assert.Single(await db.ExternalCallAttempts.Where(x => x.AiJobId == id).ToArrayAsync());
            Assert.Equal("[erased]", (await db.ContentReports.SingleAsync(x => x.Id == reportId)).Reason);
            Assert.Null((await db.ContentReports.SingleAsync(x => x.Id == reportId)).TargetId);
            Assert.Equal("[erased]", (await db.AdminAudits.SingleAsync(x => x.TargetId == reportId)).Reason);
        }
        finally { fixture.Clock.Advance(TimeSpan.FromDays(-8)); }
    }
}
