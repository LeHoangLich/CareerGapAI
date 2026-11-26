using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Accounts;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Integrations;
using CareerGap.Api.Matching;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class StageBackendFixesTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handler(request, ct);
    }

    [Fact]
    public async Task BE01_CandidateCannotApplyToJobOfSuspendedRecruiter()
    {
        var (candidate, _) = await fixture.User("Candidate");
        var (recruiter, recruiterId) = await fixture.User("Recruiter");
        var (admin, _) = await fixture.User("Admin");

        var upload = await FoundationFixture.Upload(candidate);
        var resumeVersionId = upload.GetProperty("versionId").GetGuid();

        var companyResp = await recruiter.PostAsJsonAsync("/api/companies", new { name = "Suspended Co", websiteUrl = "https://example.invalid", location = "Hà Nội" });
        Assert.Equal(HttpStatusCode.Created, companyResp.StatusCode);
        var companyId = (await companyResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var jobContent = new JobContentRequest("Backend Engineer", "Build APIs", "C# and PostgreSQL", null, "CNTT", "Hà Nội", "full_time", "remote", null, null, null, null, null, null, "vi");
        var jobResp = await recruiter.PostAsJsonAsync("/api/jobs", new JobCreateRequest(companyId, null, jobContent));
        Assert.Equal(HttpStatusCode.Created, jobResp.StatusCode);
        var job = await jobResp.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await fixture.PublishWithVerifiedBusinessFixture(recruiter, jobId, new { rowVersion = job.GetProperty("rowVersion").GetInt64() })).StatusCode);

        // Admin suspends recruiter; job still has status=published, so SubmitAsync must block the application.
        var suspend = await admin.PostAsJsonAsync($"/api/admin/accounts/{recruiterId}", new { action = "suspend", reason = "Tạm khóa kiểm tra BE-01" });
        Assert.Equal(HttpStatusCode.NoContent, suspend.StatusCode);

        var apply = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, apply.StatusCode);
    }

    [Fact]
    public async Task BE02_AccountPurgeRemovesP2P3QuickMatchRewriteHistoryViewsSearchesAndLearningTopic()
    {
        const string password = "BE02-Purge!Password-2026";
        var (candidate, candidateId) = await fixture.User("Candidate", password);
        var (recruiter, recruiterId) = await fixture.User("Recruiter", password);

        // Candidate creates QuickMatch (QuickMatchJds), LearningRequest, CommunitySearch, AnalysisHistoryHide, JobView.
        var quickResp = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", new QuickMatchRequest(
            "paste", null, "Kỹ sư .NET có 4 năm kinh nghiệm C#, PostgreSQL và Docker.",
            "paste", null, "Senior .NET", "Yêu cầu: C#, PostgreSQL, Docker", null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Accepted, quickResp.StatusCode);
        var quickJobId = (await quickResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Recruiter creates company + job + rewrite_jd job.
        var compResp = await recruiter.PostAsJsonAsync("/api/companies", new { name = "BE02 Co", websiteUrl = "https://example.invalid", location = "Hà Nội" });
        var companyId = (await compResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var jobContent = new JobContentRequest("BE02 Job", "Build services", "C#\nPostgreSQL", null, "CNTT", "Hà Nội", "full_time", "remote", null, null, null, null, null, null, "vi");
        var jobResp = await recruiter.PostAsJsonAsync("/api/jobs", new JobCreateRequest(companyId, null, jobContent));
        var jobDto = (await jobResp.Content.ReadFromJsonAsync<JsonElement>());
        var jobId = jobDto.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await fixture.PublishWithVerifiedBusinessFixture(recruiter, jobId, new { rowVersion = jobDto.GetProperty("rowVersion").GetInt64() })).StatusCode);

        var rewriteResp = await recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/rewrite-proposals", new { idempotencyKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Accepted, rewriteResp.StatusCode);
        var rewriteJobId = (await rewriteResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using (var scope = fixture.App.Services.CreateScope())
        {
            var js = scope.ServiceProvider.GetRequiredService<JobService>();
            for (var i = 0; i < 2; i++)
            {
                var claimed = await js.Claim("be02-worker", default);
                Assert.NotNull(claimed);
                await JobWorker.Process(scope.ServiceProvider, claimed!.Id, "be02-worker", claimed.LeaseGeneration, default);
            }
        }

        // Hide quick match in history; record job view and learning request and community search.
        Assert.Equal(HttpStatusCode.NoContent, (await candidate.DeleteAsync($"/api/matching/history/{quickJobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync($"/api/jobs/{jobId}")).StatusCode);

        var candidateViewerHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(candidateId.ToString())));
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.CommunitySearches.Add(new CommunitySearch { Term = "dotnet", Viewer = candidateViewerHash, Day = DateOnly.FromDateTime(DateTime.UtcNow) });
            db.LearningRequests.Add(new LearningRequest
            {
                OwnerId = candidateId, PairJobId = quickJobId, RequirementKey = "req-1", Topic = "Private topic for purge",
                Language = "vi", Provider = "Fake", Status = "succeeded", PeriodStart = DateTimeOffset.UtcNow.AddDays(-1),
                PeriodEnd = DateTimeOffset.UtcNow.AddDays(1), IdempotencyKey = Guid.NewGuid(), InputHash = "abc123", ReservedCredits = 1, ActualCredits = 1
            });
            await db.SaveChangesAsync();
        }

        // Request deletion for both accounts, advance 8 days, and run purge worker.
        Assert.Equal(HttpStatusCode.Accepted, (await candidate.PostAsJsonAsync("/api/account/deletion-request", new { password })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await recruiter.PostAsJsonAsync("/api/account/deletion-request", new { password })).StatusCode);
        fixture.Clock.Advance(TimeSpan.FromDays(8));

        using (var scope = fixture.App.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
            while (await svc.ProcessOneDueAsync("be02-purge-worker", default)) { }

            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal(0, await db.QuickMatchJds.CountAsync(x => x.CandidateId == candidateId));
            Assert.Equal(0, await db.AnalysisHistoryHides.CountAsync(x => x.AiJobId == quickJobId));
            Assert.Equal(0, await db.CommunitySearches.CountAsync(x => x.Viewer == candidateViewerHash));
            Assert.Equal(0, await db.JobViews.CountAsync(x => x.Viewer == candidateViewerHash));
            Assert.Equal(0, await db.JobRewriteResults.CountAsync(x => x.AiJobId == rewriteJobId));
            Assert.Equal(0, await db.JobRewriteInputs.CountAsync(x => x.RecruiterId == recruiterId || x.AiJobId == rewriteJobId));
            var lr = await db.LearningRequests.SingleAsync(x => x.OwnerId == candidateId);
            Assert.Equal("[erased]", lr.Topic);
            Assert.Equal("", lr.InputHash);
            Assert.Null(lr.PairJobId);
        }
    }

    [Fact]
    public async Task BE03_AiJobsEndpointEnforcesJobRewriteAccessAndProjectsStructuredDto()
    {
        var (recruiter, _) = await fixture.User("Recruiter");
        var compResp = await recruiter.PostAsJsonAsync("/api/companies", new { name = "BE03 Co", websiteUrl = "https://example.invalid", location = "Hà Nội" });
        var companyId = (await compResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var jobContent = new JobContentRequest("BE03 Role", "Build APIs", "C# and SQL", null, "CNTT", "Hà Nội", "full_time", "remote", null, null, null, null, null, null, "vi");
        var jobResp = await recruiter.PostAsJsonAsync("/api/jobs", new JobCreateRequest(companyId, null, jobContent));
        var job = await jobResp.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = job.GetProperty("id").GetGuid();
        var rowVersion = job.GetProperty("rowVersion").GetInt64();

        var rewriteResp = await recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/rewrite-proposals", new { idempotencyKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Accepted, rewriteResp.StatusCode);
        var rewriteId = (await rewriteResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using (var scope = fixture.App.Services.CreateScope())
        {
            var js = scope.ServiceProvider.GetRequiredService<JobService>();
            var claimed = await js.Claim("be03-worker", default);
            Assert.NotNull(claimed);
            await JobWorker.Process(scope.ServiceProvider, claimed!.Id, "be03-worker", claimed.LeaseGeneration, default);
        }

        // Before archive, /api/ai/jobs/{id} returns structured JobRewriteService DTO (has sections object, not raw outputJson).
        var aiJobDto = await recruiter.GetFromJsonAsync<JsonElement>($"/api/ai/jobs/{rewriteId}");
        Assert.Equal("succeeded", aiJobDto.GetProperty("status").GetString());
        Assert.True(aiJobDto.TryGetProperty("sections", out var sections) && sections.ValueKind == JsonValueKind.Object);
        Assert.False(aiJobDto.TryGetProperty("outputJson", out _));

        // After recruiter archives the job, /api/ai/jobs/{id} must return 404 just like /api/writing/jd-proposals/{id}.
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/archive", new { rowVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/api/writing/jd-proposals/{rewriteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/api/ai/jobs/{rewriteId}")).StatusCode);
    }

    [Fact]
    public async Task BE04_BE05_QuickMatchDoesNotLeaveOrphansOnErrorsOrQuotaAndValidatesIdempotency()
    {
        var (candidate, candidateId) = await fixture.User("Candidate");

        // BE-04a: Invalid JD must not create any pasted Resume, StoredFile, or QuickMatchJd.
        var badJdResp = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", new QuickMatchRequest(
            "paste", null, "Kỹ sư React TypeScript với 3 năm kinh nghiệm phát triển web.",
            "paste", null, "Short", "too short", null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.BadRequest, badJdResp.StatusCode);

        // BE-04b: Non-existent platform job with pasted CV must not create any Resume or StoredFile.
        var missingPlatformResp = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", new QuickMatchRequest(
            "paste", null, "Kỹ sư React TypeScript với 3 năm kinh nghiệm phát triển web.",
            "platform", Guid.NewGuid(), null, null, null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, missingPlatformResp.StatusCode);

        // BE-04c: Quota exhausted during EnqueueMatch must compensate/clean up pasted Resume, StoredFile, and QuickMatchJd.
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        Guid budgetId;
        decimal originalLimit;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var period = await db.BudgetPeriods.SingleOrDefaultAsync(x => x.Service == "Fake" && x.PeriodStart == monthStart);
            if (period is null)
            {
                period = new BudgetPeriod { Service = "Fake", Unit = "test_credit", PeriodStart = monthStart, PeriodEnd = monthStart.AddMonths(1), LimitAmount = 1000m };
                db.BudgetPeriods.Add(period);
                await db.SaveChangesAsync();
            }
            budgetId = period.Id;
            originalLimit = period.LimitAmount;
            await db.BudgetPeriods.Where(x => x.Id == budgetId).ExecuteUpdateAsync(s => s.SetProperty(x => x.LimitAmount, 0m));
        }

        try
        {
            var quotaResp = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", new QuickMatchRequest(
                "paste", null, "Kỹ sư React TypeScript với 3 năm kinh nghiệm phát triển web.",
                "paste", null, "Frontend Engineer", "Yêu cầu: React, TypeScript, CSS", null, Guid.NewGuid()));
            Assert.Equal((HttpStatusCode)429, quotaResp.StatusCode);
        }
        finally
        {
            using var scope = fixture.App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.BudgetPeriods.Where(x => x.Id == budgetId).ExecuteUpdateAsync(s => s.SetProperty(x => x.LimitAmount, originalLimit));
        }

        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal(0, await db.Resumes.CountAsync(x => x.CandidateId == candidateId));
            Assert.Equal(0, await db.StoredFiles.CountAsync(x => x.OwnerUserId == candidateId));
            Assert.Equal(0, await db.QuickMatchJds.CountAsync(x => x.CandidateId == candidateId));
        }

        // BE-05: Identical retry returns 202 with same ID; mismatched payload or cross-job-type key returns 409 idempotency_conflict.
        var idemKey = Guid.NewGuid();
        var req = new QuickMatchRequest(
            "paste", null, "Kỹ sư React TypeScript với 3 năm kinh nghiệm phát triển web.",
            "paste", null, "Frontend Engineer", "Yêu cầu: React, TypeScript, CSS", null, idemKey, "career_start");
        var first = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", req);
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var replay = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", req);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal(firstId, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());

        var changedPayload = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", req with { JdText = "Yêu cầu: Java, Spring Boot" });
        Assert.Equal(HttpStatusCode.Conflict, changedPayload.StatusCode);

        // Also reusing an extract_cv key in QuickMatch must return 409.
        var upload = await FoundationFixture.Upload(candidate);
        var versionId = upload.GetProperty("versionId").GetGuid();
        var extractKey = Guid.NewGuid();
        await fixture.Enqueue(candidateId, versionId, extractKey);
        var crossType = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", req with { IdempotencyKey = extractKey });
        Assert.Equal(HttpStatusCode.Conflict, crossType.StatusCode);
    }

    [Fact]
    public async Task BE06_ReplacingOrHidingPublicResumeQueuesOldRedactedFileForCleanup()
    {
        var (owner, ownerId) = await fixture.User("Candidate");
        var upload1 = await FoundationFixture.Upload(owner, FoundationFixture.Docx("Version 1 CV with React and PostgreSQL skills."));
        var v1 = upload1.GetProperty("versionId").GetGuid();
        var rId = upload1.GetProperty("id").GetGuid();

        var pub1 = await owner.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = v1, expectedRowVersion = (long?)null });
        Assert.Equal(HttpStatusCode.OK, pub1.StatusCode);

        Guid redacted1Id;
        string redacted1Key;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var sel = await db.CandidatePublicResumes.SingleAsync(x => x.CandidateId == ownerId);
            redacted1Id = sel.RedactedFileId!.Value;
            redacted1Key = (await db.StoredFiles.SingleAsync(x => x.Id == redacted1Id)).ObjectKey;
        }

        // Upload v2 and publish it; old redacted file 1 must transition to delete_pending.
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(FoundationFixture.Docx("Version 2 CV with C# and Docker skills.")), "file", "cv-v2.docx");
        var upload2Resp = await owner.PostAsync($"/api/resumes/{rId}/versions", form);
        Assert.Equal(HttpStatusCode.Created, upload2Resp.StatusCode);
        var v2 = (await upload2Resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("versionId").GetGuid();

        var pub2 = await owner.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = v2, expectedRowVersion = 0L });
        Assert.Equal(HttpStatusCode.OK, pub2.StatusCode);
        var rowVersion2 = (await pub2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetInt64();

        Guid redacted2Id;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal("delete_pending", (await db.StoredFiles.SingleAsync(x => x.Id == redacted1Id)).Status);
            redacted2Id = (await db.CandidatePublicResumes.SingleAsync(x => x.CandidateId == ownerId)).RedactedFileId!.Value;
            Assert.NotEqual(redacted1Id, redacted2Id);
        }

        // Hide public resume; redacted file 2 must also transition to delete_pending.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/resumes/public/me?rowVersion={rowVersion2}")).StatusCode);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal("delete_pending", (await db.StoredFiles.SingleAsync(x => x.Id == redacted2Id)).Status);
        }

        // After 25h grace period, OrphanFileCleanupService deletes both old redacted files and objects.
        fixture.Clock.Advance(TimeSpan.FromHours(25));
        using (var scope = fixture.App.Services.CreateScope())
        {
            var cleanup = scope.ServiceProvider.GetRequiredService<OrphanFileCleanupService>();
            await cleanup.CleanupAsync(default);
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.False(await db.StoredFiles.AnyAsync(x => x.Id == redacted1Id || x.Id == redacted2Id));
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            await Assert.ThrowsAsync<FileNotFoundException>(() => storage.ReadAsync(redacted1Key, default));
        }
    }

    [Fact]
    public async Task BE07_BE08_ConcurrentJobRewriteRevisionCreationAndHistoryDeleteDoNotThrow500()
    {
        var (recruiter, _) = await fixture.User("Recruiter");
        var (candidate, _) = await fixture.User("Candidate");

        var compResp = await recruiter.PostAsJsonAsync("/api/companies", new { name = "BE07 Co", websiteUrl = "https://example.invalid", location = "Hà Nội" });
        var companyId = (await compResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var jobContent = new JobContentRequest("BE07 Role", "Build APIs", "Required: C#\nOptional: Docker", null, "CNTT", "Hà Nội", "full_time", "remote", null, null, null, null, null, null, "vi");
        var jobResp = await recruiter.PostAsJsonAsync("/api/jobs", new JobCreateRequest(companyId, null, jobContent));
        var job = await jobResp.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = job.GetProperty("id").GetGuid();
        var versionId = job.GetProperty("version").GetProperty("id").GetGuid();

        // BE-07: Race lazy JobDataRevision #1 creation between rewrite-proposals and revisions/extract.
        var rewriteTask1 = recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/rewrite-proposals", new { idempotencyKey = Guid.NewGuid() });
        var extractTask = recruiter.PostAsync($"/api/recruiter/jobs/{jobId}/versions/{versionId}/revisions/extract", null);
        var rewriteTask2 = recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/rewrite-proposals", new { idempotencyKey = Guid.NewGuid() });
        var results = await Task.WhenAll(rewriteTask1, extractTask, rewriteTask2);
        try
        {
            foreach (var r in results)
                Assert.True((int)r.StatusCode < 500, $"Unexpected 5xx status code: {r.StatusCode}");
        }
        finally { foreach (var r in results) r.Dispose(); }

        // BE-08: Race concurrent DELETE /api/matching/history/{id}.
        var quickResp = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", new QuickMatchRequest(
            "paste", null, "Kỹ sư .NET với 3 năm kinh nghiệm C#, PostgreSQL.",
            "paste", null, "Backend", "Yêu cầu: C#, PostgreSQL", null, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Accepted, quickResp.StatusCode);
        var quickId = (await quickResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using (var scope = fixture.App.Services.CreateScope())
        {
            var js = scope.ServiceProvider.GetRequiredService<JobService>();
            AiJob? claimed;
            do
            {
                claimed = await js.Claim("be08-worker", default);
                Assert.NotNull(claimed);
                await JobWorker.Process(scope.ServiceProvider, claimed!.Id, "be08-worker", claimed.LeaseGeneration, default);
            } while (claimed.Id != quickId);
        }

        var delTasks = await Task.WhenAll(
            candidate.DeleteAsync($"/api/matching/history/{quickId}"),
            candidate.DeleteAsync($"/api/matching/history/{quickId}"));
        try
        {
            foreach (var d in delTasks)
                Assert.True(d.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound, $"Unexpected delete status: {d.StatusCode}");
        }
        finally { foreach (var d in delTasks) d.Dispose(); }
    }

    [Fact]
    public async Task BE09_BE10_JdUrlRedirectPortBlockedAndHttpsRecoveryConfigurable()
    {
        // BE-09: Redirect to non-80/443 port on public host must be rejected with invalid_jd_url.
        var publicIp = new[] { IPAddress.Parse("93.184.216.34") };
        using var http = new HttpClient(new StubHandler((_, _) =>
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location = new Uri("http://public.example.com:8080/internal-jd");
            return Task.FromResult(redirect);
        }));
        var importer = new JdUrlImporter(http, (_, _) => Task.FromResult(publicIp));
        var ex = await Assert.ThrowsAsync<JdUrlImportException>(() => importer.FetchAsync("https://public.example.com/start", CancellationToken.None));
        Assert.Equal("invalid_jd_url", ex.Code);

        // BE-10: Configured HTTPS Recovery:BaseUrl with AllowHttpsOrigin=true sends recovery link cleanly.
        await using var httpsApp = fixture.App.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Recovery:BaseUrl"] = "https://careergap.example.com/",
                ["Recovery:AllowHttpsOrigin"] = "true"
            }));
        });
        var (_, userId) = await fixture.User("Candidate");
        using var scope = fixture.App.Services.CreateScope();
        var userEmail = (await scope.ServiceProvider.GetRequiredService<AppDb>().Users.SingleAsync(x => x.Id == userId)).Email!;

        // Use outbox from httpsApp's DI container.
        using var client = httpsApp.CreateClient();
        await FoundationFixture.Csrf(client);
        var forgot = await client.PostAsJsonAsync("/api/auth/password/forgot", new { email = userEmail });
        Assert.Equal(HttpStatusCode.NoContent, forgot.StatusCode);

        var outbox = httpsApp.Services.GetRequiredService<LocalMailOutbox>();
        Assert.Contains(outbox.Drain(), m => m.Recipient == userEmail && m.Link.StartsWith("https://careergap.example.com/?resetEmail=", StringComparison.Ordinal));

        await using var blockedHttpsApp = fixture.App.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Recovery:BaseUrl"] = "https://careergap.example.com/",
                ["Recovery:AllowHttpsOrigin"] = "false"
            }));
        });
        using var blockedClient = blockedHttpsApp.CreateClient();
        await FoundationFixture.Csrf(blockedClient);
        Assert.Equal(HttpStatusCode.NoContent, (await blockedClient.PostAsJsonAsync("/api/auth/password/forgot", new { email = userEmail })).StatusCode);
        Assert.DoesNotContain(blockedHttpsApp.Services.GetRequiredService<LocalMailOutbox>().Drain(), m => m.Recipient == userEmail);
    }

    [Fact]
    public async Task BE12_PagedEndpointsReturnStandardEnvelope()
    {
        var (candidate, _) = await fixture.User("Candidate");
        var (recruiter, _) = await fixture.User("Recruiter");
        var (admin, _) = await fixture.User("Admin");

        var upload = await FoundationFixture.Upload(candidate);
        var resumeVersionId = upload.GetProperty("versionId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId, expectedRowVersion = (long?)null })).StatusCode);

        var compResp = await recruiter.PostAsJsonAsync("/api/companies", new { name = "BE12 Co", websiteUrl = "https://example.invalid", location = "Hà Nội" });
        var companyId = (await compResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var jobContent = new JobContentRequest("BE12 Role", "Build APIs", "C# and SQL", null, "CNTT", "Hà Nội", "full_time", "remote", null, null, null, null, null, null, "vi");
        var jobResp = await recruiter.PostAsJsonAsync("/api/jobs", new JobCreateRequest(companyId, null, jobContent));
        var job = await jobResp.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await fixture.PublishWithVerifiedBusinessFixture(recruiter, jobId, new { rowVersion = job.GetProperty("rowVersion").GetInt64() })).StatusCode);

        var apply = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
        var applicationId = (await apply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await recruiter.PostAsJsonAsync($"/api/applications/{applicationId}/notes", new { body = "BE12 envelope note" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await candidate.PostAsJsonAsync("/api/reports", new { targetType = "job", targetId = jobId, reason = "BE12 report envelope test" })).StatusCode);

        static void AssertEnvelope(JsonElement page, int expectedPage, int expectedPageSize, int minTotal)
        {
            Assert.Equal(JsonValueKind.Array, page.GetProperty("items").ValueKind);
            Assert.True(page.GetProperty("total").GetInt32() >= minTotal);
            Assert.Equal(expectedPage, page.GetProperty("page").GetInt32());
            Assert.Equal(expectedPageSize, page.GetProperty("pageSize").GetInt32());
        }

        AssertEnvelope(await admin.GetFromJsonAsync<JsonElement>("/api/admin/accounts?page=1"), 1, 20, 3);
        AssertEnvelope(await admin.GetFromJsonAsync<JsonElement>("/api/admin/jobs?page=1"), 1, 20, 1);
        AssertEnvelope(await admin.GetFromJsonAsync<JsonElement>("/api/admin/reports?page=1"), 1, 20, 1);
        AssertEnvelope(await admin.GetFromJsonAsync<JsonElement>("/api/admin/audit?page=1"), 1, 20, 0);
        AssertEnvelope(await admin.GetFromJsonAsync<JsonElement>("/api/admin/businesses?page=1"), 1, 10, 1);
        AssertEnvelope(await candidate.GetFromJsonAsync<JsonElement>("/api/resumes?page=1"), 1, 20, 1);
        AssertEnvelope(await recruiter.GetFromJsonAsync<JsonElement>("/api/public/resumes?page=1"), 1, 20, 1);
        AssertEnvelope(await recruiter.GetFromJsonAsync<JsonElement>($"/api/applications/{applicationId}/notes?page=1"), 1, 20, 1);
    }

    [Fact]
    public async Task BE13_SavedRoadmapsColumnsAndCommunitySearchesTableAreNormalized()
    {
        var (candidate, candidateId) = await fixture.User("Candidate");

        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();

            await using var colCmd = conn.CreateCommand();
            colCmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'saved_roadmaps' ORDER BY ordinal_position";
            var columns = new List<string>();
            await using (var reader = await colCmd.ExecuteReaderAsync())
                while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
            Assert.Equal(["pair_id", "plan", "completed", "row_version", "updated_at"], columns);

            await using var tblCmd = conn.CreateCommand();
            tblCmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('community_searches', 'community_searchs')";
            var tables = new List<string>();
            await using (var reader = await tblCmd.ExecuteReaderAsync())
                while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
            Assert.Equal(["community_searches"], tables);
        }

        var (admin, _) = await fixture.User("Admin");
        var handle = "u" + candidateId.ToString("N")[..15];
        Assert.Equal(HttpStatusCode.OK, (await candidate.PutAsJsonAsync("/api/community/me", new { handle, displayName = "BE13 Member", bio = "Bio", links = (string?)null, showCareer = false, rowVersion = 0L })).StatusCode);

        var post = await candidate.PostAsJsonAsync("/api/community/posts", new { title = "DotNet Architecture Guide", summary = "DotNet architecture notes", body = "Testing normalized community_searches.", category = "technical", tags = new[] { "dotnet" }, imageId = (Guid?)null, submit = true, rowVersion = 0L });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var createdPost = await post.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/community/posts/{createdPost.GetProperty("id").GetGuid()}/state", new { action = "approve", reason = "BE13 normalized search check", rowVersion = createdPost.GetProperty("rowVersion").GetInt64() })).StatusCode);

        var search = await candidate.GetAsync("/api/community/posts?q=DotNet");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var trending = await candidate.GetFromJsonAsync<JsonElement>("/api/community/trending-searches");
        Assert.Contains(trending.EnumerateArray(), x => x.GetProperty("term").GetString() == "dotnet");
    }
}
