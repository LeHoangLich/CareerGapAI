using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Applications;
using CareerGap.Api.Accounts;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Matching;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class PairContractTests
{
    private static PairPayload Payload(decimal? years = null, bool review = false) => new(PairMatchingContract.Rubric,
        [new("skills[0]", "React", "document_span")],
        [new("req-1", "React", "required", years, "single", [], review, "document_text"),
         new("req-2", "Docker", "optional", null, "single", [], false, "document_text")], null, "remote", null, null, null, null);
    private static string Output(string status = "met", string quote = "React", string fact = "skills[0]") =>
        PairMatchingContract.Serialize(new PairOutput([new("req-1", status, [new(fact, quote)]), new("req-2", "no_evidence", [])]));
    [Fact] public void BackendComputesWeightedScoreAndMissingIsNotAProviderFailure()
    {
        var score = PairMatchingContract.Validate(Output(), Payload());
        Assert.Equal(66.67m, score.Score);
        Assert.Equal("no_evidence", score.Criteria[1].Status);
        Assert.Empty(score.Criteria[1].Evidence);
        Assert.Equal(33.33m, PairMatchingContract.Validate(Output("partial"), Payload()).Score);
    }
    [Theory]
    [InlineData("React expert", "skills[0]")]
    [InlineData("React", "skills[9]")]
    public void RejectsInventedQuotesOrWrongField(string quote, string field) =>
        Assert.Equal("match_output_invalid", Assert.Throws<ProviderError>(() => PairMatchingContract.Validate(Output(quote: quote, fact: field), Payload())).Code);
    [Fact] public void RejectsProviderScoreUnknownCriteriaAndMalformedJson()
    {
        foreach (var raw in new[] { "{}", "{", Output().Replace("\"criteria\":", "\"score\":100,\"criteria\":"), Output().Replace("req-2", "req-1"), Output().Replace("no_evidence", "met") })
            Assert.Throws<ProviderError>(() => PairMatchingContract.Validate(raw, Payload()));
    }
    [Fact] public void YearsAndAmbiguousRequirementsCannotBeMarkedFullyMet()
    {
        Assert.Throws<ProviderError>(() => PairMatchingContract.Validate(Output(), Payload(2)));
        Assert.Throws<ProviderError>(() => PairMatchingContract.Validate(Output(), Payload(review: true)));
        Assert.Equal(33.33m, PairMatchingContract.Validate(Output("partial"), Payload(2)).Score);
    }
    [Fact] public void DistinctTechnicalTermsDoNotCreateEvidence()
    {
        Assert.False(PairMatchingContract.HasOverlap("Java", ["JavaScript"]));
        Assert.False(PairMatchingContract.HasOverlap("SQL", ["PostgreSQL"]));
        Assert.True(PairMatchingContract.HasOverlap("React.js", ["React"]));
    }
    [Fact] public void PreferencesAreExplicitlyUnassessedAndExcludedFromDenominator()
    {
        var payload = Payload() with { Requirements = [new("req-1", "React", "required", null, "single", [], false, "document_text"),
            new("req-2", "Làm việc tại Hà Nội", "required", null, "single", [], false, "document_text")] };
        var score = PairMatchingContract.Validate(Output(), payload);
        Assert.Equal(100m, score.Score); Assert.Equal(0m, score.Criteria[1].Weight); Assert.Equal("not_assessed", score.Criteria[1].Status);
    }
}

public sealed class PairMatchingTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private const string CandidatePassword = "StageC-Synthetic!Password-2026";
    private sealed class InvalidMatchClient : FakeLlmClient
    {
        public override Task<LlmReply> MatchAsync(PairPayload payload, MethodParameters parameters, CancellationToken ct) =>
            Task.FromResult(new LlmReply("{\"criteria\":[],\"score\":100}", 10, 10));
    }
    private sealed record Setup(HttpClient Candidate, Guid CandidateId, HttpClient Recruiter, Guid RecruiterId,
        Guid Cv, Guid Job, Guid Jd, Guid CvRevision, Guid JdRevision);
    private async Task<Setup> Prepare()
    {
        var (candidate, candidateId) = await fixture.User(password: CandidatePassword); var (recruiter, recruiterId) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate, FoundationFixture.Docx());
        var cv = upload.GetProperty("versionId").GetGuid();
        Guid extract;
        using (var scope = fixture.App.Services.CreateScope()) extract = (await scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(candidateId, new(cv, Guid.NewGuid()), default)).Id;
        await Drain(extract);
        using var scope2 = fixture.App.Services.CreateScope(); var db = scope2.ServiceProvider.GetRequiredService<AppDb>();
        var original = await db.ResumeDataRevisions.SingleAsync(x => x.ResumeVersionId == cv);
        Assert.True(ResumeRevisionFactory.TryConfirm(original, candidateId, 2, new(1, ["React", "TypeScript", "PostgreSQL", "Docker"], ["Go"]), DateTimeOffset.UtcNow, out var confirmed, out _));
        db.ResumeDataRevisions.Add(confirmed!);
        var companyResponse = await recruiter.PostAsJsonAsync("/api/companies", new { name = $"Synthetic {Guid.NewGuid():N}"[..24], description = "Synthetic test", location = "Hà Nội" });
        Assert.Equal(HttpStatusCode.Created, companyResponse.StatusCode);
        var company = (await companyResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var create = await recruiter.PostAsJsonAsync("/api/jobs", new { companyId = company, applicationDeadline = DateTimeOffset.UtcNow.AddDays(10), content = new {
            title = "React Developer", description = "Synthetic frontend role", requirementsText = "Required: React\nOptional: Docker", location = "Hà Nội", employmentType = "full_time", workMode = "remote", language = "en" } });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(); var job = created.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PostAsJsonAsync($"/api/jobs/{job}/publish", new { rowVersion = created.GetProperty("rowVersion").GetInt64() })).StatusCode);
        var jd = (await db.Jobs.AsNoTracking().SingleAsync(x => x.Id == job)).CurrentVersionId!.Value;
        Assert.True(JobRevisionFactory.TryExtract(jd, null, 1, "Required: React\nOptional: Docker", DateTimeOffset.UtcNow, out var parsed, out _));
        var requirements = JsonSerializer.Deserialize<JobStructuredRequirements>(parsed!.StructuredData, OutputValidator.Json)!;
        Assert.True(JobRevisionFactory.TryConfirm(parsed, recruiterId, 2, new(1, requirements.Requirements.Select(x => new JobRequirementSelection(x.RequirementId, x.Importance)).ToArray(), []), DateTimeOffset.UtcNow, out var confirmedJd, out _));
        db.JobDataRevisions.AddRange(parsed, confirmedJd!); await db.SaveChangesAsync();
        return new(candidate, candidateId, recruiter, recruiterId, cv, job, jd, confirmed!.Id, confirmedJd!.Id);
    }
    private async Task Drain(Guid target)
    {
        for (var i = 0; i < 30; i++)
        {
            using var scope = fixture.App.Services.CreateScope(); var svc = scope.ServiceProvider.GetRequiredService<JobService>();
            var claim = await svc.Claim("pair-test", default); Assert.NotNull(claim);
            await JobWorker.Process(scope.ServiceProvider, claim.Id, "pair-test", claim.LeaseGeneration, default);
            if (claim.Id == target) return;
        }
        throw new InvalidOperationException("test_queue_not_drained");
    }
    private static async Task<Guid> Enqueue(HttpClient client, Setup data, string context = "self", Guid? application = null, Guid? key = null)
    {
        var response = await client.PostAsJsonAsync("/api/matching/pairs", new CreatePairRequest(data.Cv, data.Jd, key ?? Guid.NewGuid(), context, application));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact] public async Task PairIsDurableGroundedOwnerOnlyAndCacheHitBindsItsOwnKey()
    {
        var s = await Prepare(); var id = await Enqueue(s.Candidate, s); await Drain(id);
        var pair = await s.Candidate.GetFromJsonAsync<PairJobView>($"/api/matching/pairs/{id}");
        Assert.Equal("succeeded", pair!.Status); Assert.Equal(50m, pair.Score); Assert.Equal(s.CvRevision, pair.ResumeRevisionId);
        Assert.Equal(s.JdRevision, pair.JobRevisionId); Assert.Equal("not_assessed_no_confirmed_preferences", pair.ConditionsStatus);
        var (other, _) = await fixture.User(); var (admin, _) = await fixture.User("Admin");
        foreach (var client in new[] { other, s.Recruiter, admin }) Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/matching/pairs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Candidate.GetAsync($"/api/matching/pairs/{id}/evaluation-capture")).StatusCode);
        foreach (var client in new[] { other, s.Recruiter, admin }) Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/matching/pairs/{id}/evaluation-capture")).StatusCode);
        var key = Guid.NewGuid(); var cached = await Enqueue(s.Candidate, s, key: key);
        Assert.NotEqual(id, cached); Assert.Equal(cached, await Enqueue(s.Candidate, s, key: key));
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Empty(await db.ExternalCallAttempts.Where(x => x.AiJobId == cached).ToListAsync());
        Assert.Equal(50m, (await db.MatchResults.SingleAsync(x => x.AiJobId == cached)).Score);
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE match_results SET score = 100 WHERE ai_job_id = {id}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE match_inputs SET context = 'public_pool' WHERE ai_job_id = {id}"));
        await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_jobs SET job_type = 'extract_cv' WHERE id = {id}"));
        var conflict = await s.Candidate.PostAsJsonAsync("/api/matching/pairs", new CreatePairRequest(s.Cv, s.Jd, key, "public_pool"));
        Assert.Equal(HttpStatusCode.NotFound, conflict.StatusCode);
    }
    [Fact] public async Task PublicCacheAndGenericJobReadAreRevokedAfterHideAndDispatchIsBlocked()
    {
        var s = await Prepare(); var (otherRecruiter, _) = await fixture.User("Recruiter");
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.PostAsJsonAsync("/api/matching/pairs", new CreatePairRequest(s.Cv, s.Jd, Guid.NewGuid(), "public_pool"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Candidate.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = s.Cv, expectedRowVersion = (long?)null })).StatusCode);
        var id = await Enqueue(s.Recruiter, s, "public_pool"); await Drain(id);
        Assert.Equal(HttpStatusCode.OK, (await s.Recruiter.GetAsync($"/api/ai/jobs/{id}")).StatusCode);
        using (var scope = fixture.App.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<AppDb>().CandidatePublicResumes.Where(x => x.CandidateId == s.CandidateId).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await s.Recruiter.GetAsync($"/api/matching/pairs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Recruiter.GetAsync($"/api/ai/jobs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Recruiter.GetAsync($"/api/matching/pairs/{id}/evaluation-capture")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Recruiter.PostAsJsonAsync("/api/matching/pairs", new CreatePairRequest(s.Cv, s.Jd, Guid.NewGuid(), "public_pool"))).StatusCode);
    }
    [Fact] public async Task NewConfirmationsDoNotBackfillApplicationAndExistingKeyKeepsItsInputs()
    {
        var s = await Prepare(); var key = Guid.NewGuid(); var self = await Enqueue(s.Candidate, s, key: key); await Drain(self);
        var application = await s.Candidate.PostAsJsonAsync($"/api/jobs/{s.Job}/applications", new ApplyRequest(s.Cv, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, application.StatusCode);
        var appId = (await application.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var parent = await db.ResumeDataRevisions.SingleAsync(x => x.ResumeVersionId == s.Cv && x.RevisionNo == 1);
            Assert.True(ResumeRevisionFactory.TryConfirm(parent, s.CandidateId, 3, new(1, ["React"], ["Rust"]), DateTimeOffset.UtcNow, out var newer, out _));
            db.ResumeDataRevisions.Add(newer!); await db.SaveChangesAsync();
        }
        Assert.Equal(self, await Enqueue(s.Candidate, s, key: key));
        var id = await Enqueue(s.Recruiter, s, "application", appId); await Drain(id);
        var pair = await s.Recruiter.GetFromJsonAsync<PairJobView>($"/api/matching/pairs/{id}");
        Assert.Equal(s.CvRevision, pair!.ResumeRevisionId);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var next = new JobVersion { JobId = s.Job, CreatedBy = s.RecruiterId, VersionNo = 2, Title = "Changed JD", Description = "Synthetic changed job", RequirementsText = "Rust", ContentHash = new string('b', 64) };
            db.JobVersions.Add(next); await db.SaveChangesAsync();
            await db.Jobs.Where(x => x.Id == s.Job).ExecuteUpdateAsync(x => x.SetProperty(j => j.CurrentVersionId, next.Id));
            Assert.Equal(HttpStatusCode.NotFound, (await s.Recruiter.PostAsJsonAsync("/api/matching/pairs", new CreatePairRequest(s.Cv, next.Id, Guid.NewGuid(), "application", appId))).StatusCode);
        }
        var reanalyzed = await Enqueue(s.Recruiter, s, "application", appId);
        var pinned = await s.Recruiter.GetFromJsonAsync<PairJobView>($"/api/matching/pairs/{reanalyzed}");
        Assert.Equal(s.Jd, pinned!.JobVersionId); Assert.Equal(s.JdRevision, pinned.JobRevisionId);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Candidate.GetAsync($"/api/matching/pairs/{self}")).StatusCode);
    }
    [Fact] public async Task PublicRevocationBeforeDispatchMakesNoExternalAttempt()
    {
        var s = await Prepare();
        Assert.Equal(HttpStatusCode.OK, (await s.Candidate.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = s.Cv, expectedRowVersion = (long?)null })).StatusCode);
        var id = await Enqueue(s.Recruiter, s, "public_pool");
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.CandidatePublicResumes.Where(x => x.CandidateId == s.CandidateId).ExecuteDeleteAsync();
        await Drain(id);
        Assert.Empty(await db.ExternalCallAttempts.Where(x => x.AiJobId == id).ToArrayAsync());
        Assert.False(await db.MatchResults.AnyAsync(x => x.AiJobId == id));
        Assert.Equal("access_revoked", (await db.AiJobs.SingleAsync(x => x.Id == id)).ErrorCode);
    }
    [Fact] public async Task ApplicationWithNoConfirmedDataAtSubmissionCannotUseLaterConfirmation()
    {
        var s = await Prepare();
        var upload = await FoundationFixture.Upload(s.Candidate, FoundationFixture.Docx());
        var cv = upload.GetProperty("versionId").GetGuid();
        var application = await s.Candidate.PostAsJsonAsync($"/api/jobs/{s.Job}/applications", new ApplyRequest(cv, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, application.StatusCode);
        var appId = (await application.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Guid extraction;
        using (var scope = fixture.App.Services.CreateScope()) extraction = (await scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(s.CandidateId, new(cv, Guid.NewGuid()), default)).Id;
        await Drain(extraction);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>(); var raw = await db.ResumeDataRevisions.SingleAsync(x => x.ResumeVersionId == cv);
            Assert.True(ResumeRevisionFactory.TryConfirm(raw, s.CandidateId, 2, new(1, ["React"], []), DateTimeOffset.UtcNow, out var confirmed, out _));
            db.ResumeDataRevisions.Add(confirmed!); await db.SaveChangesAsync();
        }
        var response = await s.Recruiter.PostAsJsonAsync("/api/matching/pairs", new CreatePairRequest(cv, s.Jd, Guid.NewGuid(), "application", appId));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("confirmed_resume_required", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }
    [Fact] public async Task CandidateDeletionErasesBothOwnersMatchingPayloadsButRetainsUsage()
    {
        var s = await Prepare(); var self = await Enqueue(s.Candidate, s); await Drain(self);
        Assert.Equal(HttpStatusCode.OK, (await s.Candidate.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = s.Cv, expectedRowVersion = (long?)null })).StatusCode);
        var recruiter = await Enqueue(s.Recruiter, s, "public_pool"); await Drain(recruiter);
        Assert.Equal(HttpStatusCode.Accepted, (await s.Candidate.PostAsJsonAsync("/api/account/deletion-request", new { password = CandidatePassword })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Recruiter.GetAsync($"/api/matching/pairs/{recruiter}/evaluation-capture")).StatusCode);
        fixture.Clock.Advance(TimeSpan.FromDays(8));
        try
        {
            using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.True(await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().ProcessOneDueAsync("pair-purge", default));
            Assert.Equal("deleted", (await db.Users.AsNoTracking().SingleAsync(x => x.Id == s.CandidateId)).AccountStatus);
            foreach (var id in new[] { self, recruiter })
            {
                Assert.False(await db.MatchInputs.AnyAsync(x => x.AiJobId == id)); Assert.False(await db.MatchResults.AnyAsync(x => x.AiJobId == id));
                Assert.Null((await db.AiJobs.AsNoTracking().SingleAsync(x => x.Id == id)).OutputJson);
                Assert.Single(await db.ExternalCallAttempts.Where(x => x.AiJobId == id).ToArrayAsync());
            }
        }
        finally { fixture.Clock.Advance(TimeSpan.FromDays(-8)); }
    }
    [Fact] public async Task ApplicationUsesOnlySubmissionSnapshotsAndRetentionErasesMatchPayload()
    {
        var s = await Prepare();
        var response = await s.Candidate.PostAsJsonAsync($"/api/jobs/{s.Job}/applications", new ApplyRequest(s.Cv, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var app = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var id = await Enqueue(s.Recruiter, s, "application", app); await Drain(id);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Candidate.GetAsync($"/api/matching/pairs/{id}")).StatusCode);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var job = await db.Jobs.SingleAsync(x => x.Id == s.Job); job.Status = "closed"; job.RowVersion++; job.ClosedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await s.Recruiter.GetAsync($"/api/matching/pairs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Candidate.PostAsJsonAsync($"/api/applications/{app}/withdraw", new { rowVersion = 0 })).StatusCode);
        // Advance the test clock only; do not mutate the immutable submitted CV/JD.
        fixture.Clock.Advance(TimeSpan.FromDays(91));
        try
        {
            using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Null(await scope.ServiceProvider.GetRequiredService<PairMatchingService>().Read(s.RecruiterId, id, default));
            Assert.True(await scope.ServiceProvider.GetRequiredService<ApplicationRetentionService>().PurgeExpiredAsync(default) >= 1);
            Assert.False(await db.MatchInputs.AnyAsync(x => x.AiJobId == id)); Assert.False(await db.MatchResults.AnyAsync(x => x.AiJobId == id));
            Assert.Null((await db.AiJobs.SingleAsync(x => x.Id == id)).OutputJson);
        }
        finally { fixture.Clock.Advance(TimeSpan.FromDays(-91)); }
    }
    [Fact] public async Task RevocationBetweenDispatchAndCompletionPreventsResultButSettlesUsage()
    {
        var s = await Prepare(); var id = await Enqueue(s.Candidate, s);
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>(); var jobs = scope.ServiceProvider.GetRequiredService<JobService>();
        var claim = await jobs.Claim("lease-test", default); Assert.Equal(id, claim!.Id);
        var attempt = await jobs.StartAttempt(id, "lease-test", claim.LeaseGeneration, default); Assert.NotNull(attempt);
        var input = await db.MatchInputs.SingleAsync(x => x.AiJobId == id);
        var payload = JsonSerializer.Deserialize<PairPayload>(input.Payload, OutputValidator.Json)!;
        await db.Jobs.Where(x => x.Id == s.Job).ExecuteUpdateAsync(x => x.SetProperty(j => j.Status, "closed").SetProperty(j => j.ClosedAt, DateTimeOffset.UtcNow));
        await jobs.Settle(attempt.Value, new("{}", 2, 2), true, default);
        Assert.True(await jobs.Finish(id, "lease-test", claim.LeaseGeneration, PairMatchingContract.Serialize(PairMatchingContract.Fake(payload)), null, false, default));
        db.ChangeTracker.Clear(); var row = await db.AiJobs.SingleAsync(x => x.Id == id);
        Assert.Equal("failed", row.Status); Assert.Equal("access_revoked", row.ErrorCode); Assert.False(await db.MatchResults.AnyAsync(x => x.AiJobId == id));
        Assert.Equal(1m, (await db.ExternalCallAttempts.SingleAsync(x => x.Id == attempt)).ActualAmount);
    }
    [Fact] public async Task UserQuotaAndConcurrentAdmissionAreBoundedAndTwoDirectionsCreateSamePairContract()
    {
        var s = await Prepare();
        var response = await s.Candidate.PostAsJsonAsync("/api/matching/recommendations", new PairBatchRequest("jobs", Guid.NewGuid(), s.Cv));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var batch = await response.Content.ReadFromJsonAsync<PairBatchView>();
        Assert.InRange(batch!.Items.Count, 1, 3);
        // Other tests leave equally ranked published JDs in this fixture. Validate the
        // bounded selected set rather than assuming this test's JD wins an ID tie.
        foreach (var item in batch.Items)
        {
            Assert.Equal(s.Cv, item.ResumeVersionId); Assert.NotNull(item.PairJobId);
            await Drain(item.PairJobId.Value);
            var pair = await s.Candidate.GetFromJsonAsync<PairJobView>($"/api/matching/pairs/{item.PairJobId}");
            Assert.Equal(PairMatchingContract.Rubric, pair!.Rubric); Assert.Equal("succeeded", pair.Status);
        }
        var (admin, _) = await fixture.User("Admin");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/matching/recommendations", new PairBatchRequest("jobs", Guid.NewGuid(), s.Cv))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Candidate.PostAsJsonAsync("/api/matching/recommendations", new PairBatchRequest("jobs", Guid.NewGuid(), s.Cv, TopK: 4))).StatusCode);
        using var scope = fixture.App.Services.CreateScope(); var opt = scope.ServiceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
        var previous = opt.FakeUserTestCreditLimit; opt.FakeUserTestCreditLimit = 1;
        try
        {
            var limited = await s.Candidate.PostAsJsonAsync("/api/ai/jobs", new CreateJobRequest(s.Cv, Guid.NewGuid()));
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            Assert.Equal("user_quota_exhausted", (await limited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        }
        finally { opt.FakeUserTestCreditLimit = previous; }
    }
    [Fact] public async Task ConcurrentRequestsRespectUserAdmissionAndLostLeaseCannotPublish()
    {
        var s = await Prepare();
        using var scope = fixture.App.Services.CreateScope(); var opt = scope.ServiceProvider.GetRequiredService<IOptions<AiOptions>>().Value;
        var previous = opt.UserMaxActiveJobs; opt.UserMaxActiveJobs = 1;
        try
        {
            var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => s.Candidate.PostAsJsonAsync("/api/ai/jobs", new CreateJobRequest(s.Cv, Guid.NewGuid()))));
            Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Accepted); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.TooManyRequests);
            var id = (await responses.Single(r => r.StatusCode == HttpStatusCode.Accepted).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            await Drain(id);
            var pairId = await Enqueue(s.Candidate, s);
            var svc = scope.ServiceProvider.GetRequiredService<JobService>(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var claim = await svc.Claim("old-worker", default); Assert.Equal(pairId, claim!.Id);
            var call = await svc.StartAttempt(pairId, "old-worker", claim.LeaseGeneration, default);
            await db.AiJobs.Where(x => x.Id == pairId).ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
            db.ChangeTracker.Clear(); var replacement = await svc.Claim("new-worker", default); Assert.Equal(pairId, replacement!.Id);
            Assert.False(await svc.Finish(pairId, "old-worker", claim.LeaseGeneration, "{}", null, false, default));
            // A late response still settles once even after the replacement owns the lease.
            await svc.Settle(call!.Value, new("{}", 5, 5), false, default); await svc.Settle(call.Value, new("{}", 5, 5), false, default);
            await JobWorker.Process(scope.ServiceProvider, pairId, "new-worker", replacement.LeaseGeneration, default);
            db.ChangeTracker.Clear(); Assert.Equal("succeeded", (await db.AiJobs.SingleAsync(x => x.Id == pairId)).Status);
            Assert.Equal(2, await db.ExternalCallAttempts.CountAsync(x => x.AiJobId == pairId));
            Assert.Equal(0m, (await db.BudgetReservations.SingleAsync(x => x.AiJobId == pairId)).ReservedRemaining);
        }
        finally { opt.UserMaxActiveJobs = previous; }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettlementReloadsLockedBudgetAfterAnotherContextAdmitsAJob(bool alreadySettledElsewhere)
    {
        var s = await Prepare();
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var pairs = scope.ServiceProvider.GetRequiredService<PairMatchingService>(); var jobs = scope.ServiceProvider.GetRequiredService<JobService>();
        var pair = await pairs.Enqueue(s.CandidateId, new(s.Cv, s.Jd, Guid.NewGuid()), default);
        var claim = await jobs.Claim("interleaved-worker", default); Assert.Equal(pair.Id, claim!.Id);
        var attempt = await jobs.StartAttempt(pair.Id, "interleaved-worker", claim.LeaseGeneration, default); Assert.NotNull(attempt);
        Guid another;
        using (var other = fixture.App.Services.CreateScope())
            another = (await other.ServiceProvider.GetRequiredService<JobService>().Enqueue(s.CandidateId, new(s.Cv, Guid.NewGuid()), default)).Id;
        var input = await db.MatchInputs.SingleAsync(x => x.AiJobId == pair.Id);
        var payload = JsonSerializer.Deserialize<PairPayload>(input.Payload, OutputValidator.Json)!;
        var raw = PairMatchingContract.Serialize(PairMatchingContract.Fake(payload));
        if (alreadySettledElsewhere)
        {
            using var other = fixture.App.Services.CreateScope();
            await other.ServiceProvider.GetRequiredService<JobService>().Settle(attempt.Value, new(raw, 10, 10), true, default);
        }
        await jobs.Settle(attempt.Value, new(raw, 10, 10), true, default);
        Assert.True(await jobs.Finish(pair.Id, "interleaved-worker", claim.LeaseGeneration, raw, null, false, default));
        using (var verify = fixture.App.Services.CreateScope())
        {
            var fresh = verify.ServiceProvider.GetRequiredService<AppDb>();
            var budget = await fresh.BudgetPeriods.SingleAsync(x => x.Service == "Fake");
            Assert.Equal(await fresh.BudgetReservations.Where(x => x.BudgetPeriodId == budget.Id).SumAsync(x => x.ReservedRemaining), budget.ReservedAmount);
            Assert.Equal(await fresh.ExternalCallAttempts.Where(x => x.ActualAmount != null).SumAsync(x => x.ActualAmount!.Value), budget.SpentAmount);
        }
        await Drain(another);
    }
    [Fact] public async Task InvalidProviderOutputRetriesOnceAndNeverBecomesAZeroScore()
    {
        var s = await Prepare(); var id = await Enqueue(s.Candidate, s);
        using var sourceScope = fixture.App.Services.CreateScope();
        var storagePath = sourceScope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Storage:Root"]!;
        using var failing = new TestApp(fixture.Connection, storagePath, new InvalidMatchClient());
        for (var i = 0; i < 2; i++)
        {
            using var scope = failing.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.AiJobs.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AvailableAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
            var claim = await scope.ServiceProvider.GetRequiredService<JobService>().Claim("invalid-worker", default); Assert.Equal(id, claim!.Id);
            await JobWorker.Process(scope.ServiceProvider, id, "invalid-worker", claim.LeaseGeneration, default);
        }
        var view = await s.Candidate.GetFromJsonAsync<PairJobView>($"/api/matching/pairs/{id}");
        Assert.Equal("failed", view!.Status); Assert.Equal("match_output_invalid", view.ErrorCode); Assert.Null(view.Score); Assert.Null(view.Criteria);
        var check = sourceScope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal(2, await check.ExternalCallAttempts.CountAsync(x => x.AiJobId == id));
        Assert.Equal(0m, (await check.BudgetReservations.SingleAsync(x => x.AiJobId == id)).ReservedRemaining);
    }
}
