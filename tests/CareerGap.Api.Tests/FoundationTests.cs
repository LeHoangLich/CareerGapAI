using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class FoundationTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task PrivateFilesAndJobsCannotBeReadByOtherRolesOrOwners()
    {
        var (owner, uid) = await fixture.User();
        var file = await FoundationFixture.Upload(owner);
        var fileId = file.GetProperty("fileId").GetGuid();
        var version = file.GetProperty("versionId").GetGuid();
        var job = await fixture.Enqueue(uid, version);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var guard = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_jobs SET input_hash = repeat('a',64) WHERE id = {job.Id}"));
            Assert.Equal("23514", guard.SqlState);
        }
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/files/{fileId}")).StatusCode);
        foreach (var role in new[] { "Candidate", "Recruiter", "Admin" })
        {
            var (other, _) = await fixture.User(role);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/files/{fileId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/ai/jobs/{job.Id}")).StatusCode);
            var response = await other.PostAsJsonAsync("/api/ai/jobs", new CreateJobRequest(version, Guid.NewGuid()));
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden });
        }
        var anon = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync($"/api/files/{fileId}")).StatusCode);
    }
    [Fact]
    public async Task CsrfProtectsLoginLogoutAndUpload()
    {
        var anon = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await anon.PostAsJsonAsync("/api/auth/login", new { email = "fake@example.invalid", password = "arbitrary" })).StatusCode);
        var (user, _) = await fixture.User();
        user.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsync("/api/auth/logout", null)).StatusCode);
        using var content = new MultipartFormDataContent(); content.Add(new ByteArrayContent(FoundationFixture.Docx()), "file", "fake.docx");
        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsync("/api/resumes", content)).StatusCode);
        await FoundationFixture.Csrf(user);
        Assert.Equal(HttpStatusCode.NoContent, (await user.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.False((await user.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("authenticated").GetBoolean());
    }
    [Fact]
    public async Task NullLoginFieldsReturnValidationErrorInsteadOfServerError()
    {
        var client = fixture.App.CreateClient();
        await FoundationFixture.Csrf(client);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = (string?)null, password = (string?)null });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationCreatesOnlyAllowedRolesAndCandidateProfileAtomically()
    {
        var csrfless = fixture.App.CreateClient();
        var csrflessEmail = $"csrf-register-{Guid.NewGuid():N}@example.invalid";
        var csrflessRequest = new { email = csrflessEmail, password = "Synthetic!Password2026", role = "Candidate" };
        Assert.Equal(HttpStatusCode.BadRequest, (await csrfless.PostAsJsonAsync("/api/auth/register", csrflessRequest)).StatusCode);
        await FoundationFixture.Csrf(csrfless);
        Assert.Equal(HttpStatusCode.Created, (await csrfless.PostAsJsonAsync("/api/auth/register", csrflessRequest)).StatusCode);

        var client = fixture.App.CreateClient();
        await FoundationFixture.Csrf(client);
        var email = $"register-{Guid.NewGuid():N}@example.invalid";
        const string password = "Synthetic!Password2026";
        var registered = await client.PostAsJsonAsync("/api/auth/register", new { email, password, role = "Candidate", displayName = "Ứng viên giả" });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/auth/register", new { email, password, role = "Recruiter" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { email = $"admin-{Guid.NewGuid():N}@example.invalid", password, role = "Admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { email = $"weak-{Guid.NewGuid():N}@example.invalid", password = "short", role = "Candidate" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { email = $"bad-{Guid.NewGuid():N}@example.invalid", password, role = "Candidate", displayName = "x".PadRight(151, 'x') })).StatusCode);

        await FoundationFixture.Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
        var session = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Contains("Candidate", session.GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/profile/me");
        Assert.Equal("Ứng viên giả", profile.GetProperty("displayName").GetString());
        Assert.Equal(0, profile.GetProperty("rowVersion").GetInt64());

        var recruiter = fixture.App.CreateClient();
        await FoundationFixture.Csrf(recruiter);
        var recruiterEmail = $"recruiter-{Guid.NewGuid():N}@example.invalid";
        Assert.Equal(HttpStatusCode.Created, (await recruiter.PostAsJsonAsync("/api/auth/register", new { email = recruiterEmail, password, role = "Recruiter" })).StatusCode);
        await FoundationFixture.Csrf(recruiter);
        Assert.Equal(HttpStatusCode.NoContent, (await recruiter.PostAsJsonAsync("/api/auth/login", new { email = recruiterEmail, password })).StatusCode);
        Assert.Contains("Recruiter", (await recruiter.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("roles").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync("/api/profile/me")).StatusCode);
    }

    [Fact]
    public async Task CandidateProfileReadsAreOwnerOnlyAndRecruiterDtoFiltersUnsharedFields()
    {
        var (owner, ownerId) = await fixture.User();
        var (otherCandidate, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var (admin, _) = await fixture.User("Admin");

        var before = await owner.GetFromJsonAsync<JsonElement>("/api/profile/me");
        var update = new
        {
            rowVersion = before.GetProperty("rowVersion").GetInt64(),
            displayName = "Tên ứng viên", headline = "Backend Developer", summary = "Tóm tắt riêng",
            location = "Hà Nội", contactEmail = "private@example.invalid", contactPhone = "0900000000",
            desiredRole = "Backend", preferredWorkMode = "hybrid", experienceYears = 3.25m,
            details = new { skills = new[] { "C#", "PostgreSQL" }, projects = new[] { "Dự án riêng" } },
            sharedFields = new[] { "display_name", "headline", "skills" }
        };
        var saved = await owner.PutAsJsonAsync("/api/profile/me", update);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(1, (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync("/api/profile/me", update)).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await otherCandidate.GetAsync($"/api/profiles/{ownerId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/profiles/{ownerId}")).StatusCode);
        var publicView = await recruiter.GetFromJsonAsync<JsonElement>($"/api/profiles/{ownerId}");
        Assert.Equal("Tên ứng viên", publicView.GetProperty("displayName").GetString());
        Assert.Equal("Backend Developer", publicView.GetProperty("headline").GetString());
        Assert.Equal(JsonValueKind.Null, publicView.GetProperty("contactEmail").ValueKind);
        Assert.Equal(JsonValueKind.Null, publicView.GetProperty("contactPhone").ValueKind);
        Assert.False(publicView.TryGetProperty("userId", out _));
        Assert.True(publicView.GetProperty("details").TryGetProperty("skills", out _));
        Assert.False(publicView.GetProperty("details").TryGetProperty("projects", out _));

        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.PutAsJsonAsync("/api/profile/me", update)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync("/api/profile/me", new
        {
            rowVersion = 1L, displayName = update.displayName, headline = update.headline, summary = update.summary,
            location = update.location, contactEmail = update.contactEmail, contactPhone = update.contactPhone,
            desiredRole = update.desiredRole, preferredWorkMode = update.preferredWorkMode, experienceYears = update.experienceYears,
            details = update.details, sharedFields = new[] { "password_hash" }
        })).StatusCode);
    }

    [Fact]
    public async Task PasswordChangeAndFakeRecoveryUseSingleUseIdentityTokenWithoutEmailEnumeration()
    {
        var client = fixture.App.CreateClient();
        await FoundationFixture.Csrf(client);
        var email = $"recovery-{Guid.NewGuid():N}@example.invalid";
        const string originalPassword = "Synthetic!Password2026";
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { email, password = originalPassword, role = "Candidate" })).StatusCode);
        await FoundationFixture.Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = originalPassword })).StatusCode);
        await FoundationFixture.Csrf(client);
        const string changedPassword = "Changed!Password2026";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/password/change", new { currentPassword = originalPassword, newPassword = changedPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password/change", new { currentPassword = originalPassword, newPassword = "Another!Password2026" })).StatusCode);

        var request = fixture.App.CreateClient();
        await FoundationFixture.Csrf(request);
        Assert.Equal(HttpStatusCode.NoContent, (await request.PostAsJsonAsync("/api/auth/password/forgot", new { email })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await request.PostAsJsonAsync("/api/auth/password/forgot", new { email = "unknown@example.invalid" })).StatusCode);
        var outbox = await request.GetFromJsonAsync<JsonElement[]>("/api/dev/mailbox");
        var recoveryLink = new Uri(outbox!.Single().GetProperty("link").GetString()!);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(recoveryLink.Query);
        var token = query["resetToken"].ToString();
        var resetEmail = query["resetEmail"].ToString();
        const string resetPassword = "Reset!Password2026";
        await FoundationFixture.Csrf(request);
        Assert.Equal(HttpStatusCode.BadRequest, (await request.PostAsJsonAsync("/api/auth/password/reset", new { email = resetEmail, token = "invalid-token", newPassword = resetPassword })).StatusCode);
        await Task.Delay(TimeSpan.FromSeconds(6));
        await FoundationFixture.Csrf(request);
        Assert.Equal(HttpStatusCode.BadRequest, (await request.PostAsJsonAsync("/api/auth/password/reset", new { email = resetEmail, token, newPassword = resetPassword })).StatusCode);
        await FoundationFixture.Csrf(request);
        Assert.Equal(HttpStatusCode.NoContent, (await request.PostAsJsonAsync("/api/auth/password/forgot", new { email })).StatusCode);
        var renewed = await request.GetFromJsonAsync<JsonElement[]>("/api/dev/mailbox");
        var renewedLink = new Uri(renewed!.Single().GetProperty("link").GetString()!);
        var renewedQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(renewedLink.Query);
        var renewedToken = renewedQuery["resetToken"].ToString();
        var renewedEmail = renewedQuery["resetEmail"].ToString();
        await FoundationFixture.Csrf(request);
        Assert.Equal(HttpStatusCode.NoContent, (await request.PostAsJsonAsync("/api/auth/password/reset", new { email = renewedEmail, token = renewedToken, newPassword = resetPassword })).StatusCode);
        await FoundationFixture.Csrf(request);
        Assert.Equal(HttpStatusCode.BadRequest, (await request.PostAsJsonAsync("/api/auth/password/reset", new { email = renewedEmail, token = renewedToken, newPassword = "Reused!Password2026" })).StatusCode);
        await FoundationFixture.Csrf(request);
        Assert.Equal(HttpStatusCode.Unauthorized, (await request.PostAsJsonAsync("/api/auth/login", new { email, password = originalPassword })).StatusCode);
        await FoundationFixture.Csrf(request);
        Assert.Equal(HttpStatusCode.NoContent, (await request.PostAsJsonAsync("/api/auth/login", new { email, password = resetPassword })).StatusCode);
    }
    [Fact]
    public async Task RepeatedKeyReturnsSameJobAndDifferentInputConflicts()
    {
        var (user, uid) = await fixture.User();
        var a = await FoundationFixture.Upload(user);
        var b = await FoundationFixture.Upload(user);
        var key = Guid.NewGuid();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => fixture.Enqueue(uid, a.GetProperty("versionId").GetGuid(), key)));
        Assert.Single(results.Select(x => x.Id).Distinct());
        var error = await Assert.ThrowsAsync<JobError>(() => fixture.Enqueue(uid, b.GetProperty("versionId").GetGuid(), key));
        Assert.Equal(409, error.Status);
    }
    [Fact]
    public async Task WorkerProducesValidatedFakeOutputAndReconcilesOnce()
    {
        var (user, uid) = await fixture.User();
        var file = await FoundationFixture.Upload(user);
        var job = await fixture.Enqueue(uid, file.GetProperty("versionId").GetGuid());
        using var scope = fixture.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<JobService>();
        AiJob? claimed;
        // Other tests may leave queued jobs; process them to reach this job.
        do
        {
            claimed = await service.Claim("worker-test", default); Assert.NotNull(claimed);
            await JobWorker.Process(scope.ServiceProvider, claimed.Id, "worker-test", claimed.LeaseGeneration, default);
        } while (claimed.Id != job.Id);
        var db = scope.ServiceProvider.GetRequiredService<AppDb>(); db.ChangeTracker.Clear();
        var saved = await db.AiJobs.SingleAsync(x => x.Id == job.Id);
        Assert.Equal("succeeded", saved.Status);
        Assert.Contains("React", saved.OutputJson);
        var revision = await db.ResumeDataRevisions.SingleAsync(x => x.AiJobId == job.Id);
        Assert.Equal("ai_extracted", revision.Origin);
        Assert.Equal(1, revision.RevisionNo);
        Assert.Equal(DocumentParser.Version + "+" + ResumeRevisionFactory.StructuredParserVersion, revision.ParserVersion);
        Assert.Contains("Synthetic testing only", revision.SourceText);
        Assert.Contains("React", revision.StructuredData);
        Assert.Contains("document_span", revision.Evidence);
        var attempt = await db.ExternalCallAttempts.SingleAsync(x => x.AiJobId == job.Id);
        var reservation = await db.BudgetReservations.SingleAsync(x => x.AiJobId == job.Id);
        Assert.Equal(0m, reservation.ReservedRemaining);
        await service.Settle(attempt.Id, new LlmReply("{}", 0, 0), true, default);
        Assert.Equal(1m, (await db.ExternalCallAttempts.AsNoTracking().SingleAsync(x => x.Id == attempt.Id)).ActualAmount);
    }
    [Fact]
    public async Task ExpiredWorkerCannotPublishAndUnknownCostsStayReserved()
    {
        var (user, uid) = await fixture.User();
        var file = await FoundationFixture.Upload(user);
        var job = await fixture.Enqueue(uid, file.GetProperty("versionId").GetGuid());
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // Isolate this job from other queued tests without changing their budgets.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_jobs SET available_at = now() + interval '1 day' WHERE status = 'queued' AND id <> {job.Id}");
        var svc = scope.ServiceProvider.GetRequiredService<JobService>();
        var first = await svc.Claim("old", default); Assert.Equal(job.Id, first!.Id);
        var attempt = await svc.StartAttempt(job.Id, "old", first.LeaseGeneration, default); Assert.NotNull(attempt);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_jobs SET lease_until = now() - interval '1 second' WHERE id = {job.Id}");
        db.ChangeTracker.Clear();
        var second = await svc.Claim("new", default); Assert.NotNull(second);
        Assert.True(second.LeaseGeneration > first.LeaseGeneration);
        Assert.False(await svc.Finish(job.Id, "old", first.LeaseGeneration, "{}", null, false, default));
        Assert.True(await svc.Finish(job.Id, "new", second.LeaseGeneration, null, "test_stop", false, default));
        db.ChangeTracker.Clear();
        var reservation = await db.BudgetReservations.SingleAsync(x => x.AiJobId == job.Id);
        Assert.Equal(1m, reservation.ReservedRemaining);
        Assert.Equal(0m, reservation.UnallocatedAmount);
        Assert.Equal("outcome_unknown", (await db.ExternalCallAttempts.SingleAsync(x => x.Id == attempt)).Status);
        // Late usage from old worker can settle cost once, without publishing its stale output.
        await svc.Settle(attempt!.Value, new LlmReply("{}", 0, 0), false, default);
        Assert.Equal("failed", (await db.AiJobs.AsNoTracking().SingleAsync(x => x.Id == job.Id)).Status);
        await db.Database.ExecuteSqlRawAsync("UPDATE ai_jobs SET available_at = now() WHERE status = 'queued'");
    }
    [Fact]
    public async Task CompetingAdmissionsCannotOverspendBudget()
    {
        var (user, uid) = await fixture.User();
        var file = await FoundationFixture.Upload(user);
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        // Create the current period if necessary; then leave room for exactly one two-call reservation.
        await fixture.Enqueue(uid, file.GetProperty("versionId").GetGuid());
        var period = await db.BudgetPeriods.SingleAsync(x => x.Service == "Fake");
        var prior = period.LimitAmount;
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE budget_periods SET limit_amount = spent_amount + reserved_amount + 2 WHERE id = {period.Id}");
        async Task<bool> TryEnqueue()
        {
            try { await fixture.Enqueue(uid, file.GetProperty("versionId").GetGuid()); return true; }
            catch (JobError ex) when (ex.Status == 429) { return false; }
        }
        try { Assert.Single(await Task.WhenAll(TryEnqueue(), TryEnqueue(), TryEnqueue()), x => x); }
        finally { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE budget_periods SET limit_amount = {prior} WHERE id = {period.Id}"); }
    }
    [Fact]
    public async Task DatabaseRejectsCrossOwnerReferencesAndVersionEdits()
    {
        var (user, _) = await fixture.User();
        var (_, otherUid) = await fixture.User();
        var file = await FoundationFixture.Upload(user);
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var version = file.GetProperty("versionId").GetGuid();
        var ownerEdit = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE resume_versions SET candidate_id = {otherUid} WHERE id = {version}"));
        Assert.Equal("23514", ownerEdit.SqlState);
        var fileId = file.GetProperty("fileId").GetGuid();
        var crossFile = new StoredFile { OwnerUserId = otherUid, ObjectKey = Guid.NewGuid().ToString("N"), OriginalName = "x", MimeType = "x", SizeBytes = 1 };
        db.StoredFiles.Add(crossFile); await db.SaveChangesAsync();
        var resumeId = file.GetProperty("id").GetGuid();
        var fail = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO resume_versions (id, resume_id, candidate_id, file_id, version_no, document_hash, created_at) SELECT {Guid.NewGuid()}, resume_id, candidate_id, {crossFile.Id}, 2, document_hash, now() FROM resume_versions WHERE id = {version}"));
        // The resume-version readiness trigger rejects the cross-owner file before the composite FK is evaluated.
        Assert.Equal("23514", fail.SqlState);
        var guard = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE stored_files SET sha256 = repeat('a',64) WHERE id = {fileId}"));
        Assert.Equal("23514", guard.SqlState);
    }
    [Fact]
    public void ParserReadsVietnameseAndTablesAndRejectsInvalidInput()
    {
        var parser = new DocumentParser();
        var doc = parser.Parse(FoundationFixture.Docx(), "fake.docx", default);
        Assert.Contains(doc.Segments, x => x.Text.Contains("Kỹ năng"));
        Assert.Contains(doc.Segments, x => x.Text.Contains("kiểm thử bảng"));
        Assert.Equal("file_format", Assert.Throws<DocumentError>(() => parser.Parse("not pdf"u8.ToArray(), "fake.pdf", default)).Code);
        Assert.Equal("file_size", Assert.Throws<DocumentError>(() => parser.Parse(new byte[DocumentParser.MaxBytes + 1], "fake.docx", default)).Code);
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using var entry = zip.CreateEntry("bomb").Open(); entry.Write(new byte[21 * 1024 * 1024]);
        }
        Assert.Equal("docx_expansion_limit", Assert.Throws<DocumentError>(() => parser.Parse(stream.ToArray(), "fake.docx", default)).Code);
    }
    [Theory]
    [InlineData("{broken")]
    [InlineData("{\"skills\":[{\"name\":\"Docker\",\"evidenceRef\":\"missing\",\"quote\":\"Docker\"}]}")]
    [InlineData("{\"skills\":[],\"score\":0}")]
    [InlineData("{\"skills\":[{\"name\":\"Python\",\"evidenceRef\":\"p1\",\"quote\":\"React\"}]}")]
    public void InvalidOutputNeverBecomesAnAiResult(string json)
    {
        Assert.Throws<ProviderError>(() => OutputValidator.Validate(json, new ParsedDocument("x", [new("p1", "React experience")])));
    }
    [Fact]
    public async Task CookieAndFilesSurviveApplicationRestart()
    {
        var (user, uid) = await fixture.User();
        var file = await FoundationFixture.Upload(user);
        var csrfResponse = await user.GetAsync("/api/auth/csrf");
        // Extract the cookie container indirectly by making the current server expose request Cookie to a delegating handler is unnecessary:
        // authenticate a fresh app using the same DP database and replay the existing auth cookie from login response.
        using var scope = fixture.App.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
        var appUser = await users.FindByIdAsync(uid.ToString());
        var password = "R!" + Guid.NewGuid().ToString("N");
        Assert.True((await users.ResetPasswordAsync(appUser!, await users.GeneratePasswordResetTokenAsync(appUser!), password)).Succeeded);
        var fresh = fixture.App.CreateClient(); await FoundationFixture.Csrf(fresh);
        var login = await fresh.PostAsJsonAsync("/api/auth/login", new { email = appUser!.Email, password });
        var cookie = login.Headers.GetValues("Set-Cookie").First(x => x.StartsWith("CareerGap.Session=")).Split(';')[0];
        using var restarted = new TestApp(fixture.Connection, Path.Combine(Path.GetTempPath(), new NpgsqlConnectionStringBuilder(fixture.Connection).Database!));
        var replay = restarted.CreateClient(); replay.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.True((await replay.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("authenticated").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await replay.GetAsync($"/api/files/{file.GetProperty("fileId").GetGuid()}")).StatusCode);
    }
    [Fact]
    public async Task MigrationsUpgradeDataAndDownRemovesGuards()
    {
        // Use a separate uniquely named database; never downgrade the shared test fixture or local dev DB.
        var cs = new NpgsqlConnectionStringBuilder(fixture.Connection);
        var name = "careergap_test_upgrade_" + Guid.NewGuid().ToString("N");
        cs.Database = "postgres";
        await using var admin = new NpgsqlConnection(cs.ConnectionString); await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {name}", admin).ExecuteNonQueryAsync();
        cs.Database = name;
        try
        {
            await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(cs.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261015025051_Foundation");
            var user = new AppUser { Id = Guid.NewGuid(), UserName = "fake-upgrade@example.invalid", Email = "fake-upgrade@example.invalid" };
            db.Users.Add(user); await db.SaveChangesAsync();
            await migrator.MigrateAsync();
            Assert.True(await db.Users.AnyAsync(x => x.Id == user.Id));
            await migrator.MigrateAsync("20261015025051_Foundation");
            Assert.True(await db.Users.AnyAsync(x => x.Id == user.Id));
            await migrator.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
        }
        finally { NpgsqlConnection.ClearAllPools(); await new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", admin).ExecuteNonQueryAsync(); }
    }

    [Theory]
    [InlineData(false, "ai_output_invalid")]
    [InlineData(true, "provider_timeout")]
    public async Task WorkerErrorsRetryOnceAndNeverPublishFabricatedResults(bool timeout, string code)
    {
        var (user, uid) = await fixture.User();
        var file = await FoundationFixture.Upload(user);
        var job = await fixture.Enqueue(uid, file.GetProperty("versionId").GetGuid());
        using var faultApp = new TestApp(fixture.Connection, Path.Combine(Path.GetTempPath(), new NpgsqlConnectionStringBuilder(fixture.Connection).Database!), new FaultClient(timeout));
        using var scope = faultApp.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var svc = scope.ServiceProvider.GetRequiredService<JobService>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_jobs SET available_at = now() + interval '1 day' WHERE status = 'queued' AND id <> {job.Id}");
        try
        {
            for (var i = 0; i < 2; i++)
            {
                db.ChangeTracker.Clear();
                var claimed = await svc.Claim("fault-worker", default); Assert.NotNull(claimed); Assert.Equal(job.Id, claimed.Id);
                await JobWorker.Process(scope.ServiceProvider, job.Id, "fault-worker", claimed.LeaseGeneration, default);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_jobs SET available_at = now() WHERE id = {job.Id}");
            }
            db.ChangeTracker.Clear();
            var saved = await db.AiJobs.SingleAsync(x => x.Id == job.Id);
            Assert.Equal("failed", saved.Status); Assert.Equal(code, saved.ErrorCode); Assert.Null(saved.OutputJson);
            Assert.Equal(2, await db.ExternalCallAttempts.CountAsync(x => x.AiJobId == job.Id));
            var reservation = await db.BudgetReservations.SingleAsync(x => x.AiJobId == job.Id);
            Assert.Equal(timeout ? 2m : 0m, reservation.ReservedRemaining);
        }
        finally { await db.Database.ExecuteSqlRawAsync("UPDATE ai_jobs SET available_at = now() WHERE status = 'queued'"); }
    }
    private sealed class FaultClient(bool timeout) : FakeLlmClient
    {
        public override Task<LlmReply> ExtractAsync(ParsedDocument doc, MethodParameters parameters, CancellationToken ct) =>
            timeout ? throw new TaskCanceledException() : Task.FromResult(new LlmReply("{malformed", 0, 0));
    }

    [Fact]
    public async Task SuspendedAccountImmediatelyLosesFileAndJobAccess()
    {
        var (user, uid) = await fixture.User();
        var file = await FoundationFixture.Upload(user);
        var job = await fixture.Enqueue(uid, file.GetProperty("versionId").GetGuid());
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Users.Where(x => x.Id == uid).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "suspended"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync($"/api/files/{file.GetProperty("fileId").GetGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync($"/api/ai/jobs/{job.Id}")).StatusCode);
    }

    [Fact]
    public async Task FakeParserDoesNotInferSqlFromPostgresqlOrJavaFromJavascript()
    {
        var doc = new ParsedDocument("x", [new("p1", "PostgreSQL and JavaScript are explicit. No other languages are claimed.")]);
        var result = await new FakeLlmClient().ExtractAsync(doc, new("fake", 1000, 512, 0, 0, "n/a"), default);
        var output = OutputValidator.Validate(result.Json, doc);
        Assert.Single(output.Skills); Assert.Equal("PostgreSQL", output.Skills[0].Name);
    }
}
