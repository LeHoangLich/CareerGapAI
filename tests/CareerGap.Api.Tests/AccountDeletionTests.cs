using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Accounts;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

[CollectionDefinition("Account deletion lifecycle", DisableParallelization = true)]
public sealed class AccountDeletionCollection { }

[Collection("Account deletion lifecycle")]
public sealed class AccountDeletionTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private const string Password = "L08-Testing!Password-2026";
    private const string ActiveCandidatePassword = "L08-Applicant!Password-2026";

    [Fact]
    public async Task RequestImmediatelyRevokesAccessAndPasswordConfirmedCancellationDoesNotRepublishCv()
    {
        var (candidate, candidateId) = await fixture.User(password: Password);
        var (otherCandidate, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate, FoundationFixture.Docx("Synthetic account deletion CV content."));
        var versionId = upload.GetProperty("versionId").GetGuid();
        var fileId = upload.GetProperty("fileId").GetGuid();
        using var publish = await candidate.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = versionId, expectedRowVersion = (long?)null });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        _ = await publish.Content.ReadFromJsonAsync<JsonElement>();
        var (jobId, _) = await CreatePublishedJob(recruiter, "Retention application");
        using var apply = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId = versionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
        var applicationId = (await apply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var initialSnapshot = await recruiter.GetAsync($"/api/applications/{applicationId}/resume");
        Assert.Equal(HttpStatusCode.OK, initialSnapshot.StatusCode);

        using var staleSession = fixture.App.CreateClient();
        await FoundationFixture.Csrf(staleSession);
        // Create another valid session so the security-stamp revocation is exercised independently.
        var sessionEmail = await ReadUserEmail(candidateId);
        using (var login = await staleSession.PostAsJsonAsync("/api/auth/login", new { email = sessionEmail, password = Password }))
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        await FoundationFixture.Csrf(staleSession);

        using var wrongPassword = await candidate.PostAsJsonAsync("/api/account/deletion-request", new { password = "wrong-password" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal("active", (await db.Users.SingleAsync(x => x.Id == candidateId)).AccountStatus);
            Assert.Equal(0, await db.AccountDeletionRequests.CountAsync(x => x.UserId == candidateId));
        }

        using var request = await candidate.PostAsJsonAsync("/api/account/deletion-request", new { password = Password });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        var deletionState = await request.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fixture.Clock.GetUtcNow().AddDays(7), deletionState.GetProperty("purgeAfter").GetDateTimeOffset());
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync("/api/profile/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await candidate.GetAsync($"/api/files/{fileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staleSession.GetAsync("/api/profile/me")).StatusCode);
        var otherPublic = await otherCandidate.GetFromJsonAsync<JsonElement[]>("/api/public/resumes?page=1");
        Assert.DoesNotContain(otherPublic!, x => x.GetProperty("candidateId").GetGuid() == candidateId);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCandidate.GetAsync($"/api/public/resumes/{candidateId}/{versionId}/file")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/api/applications/{applicationId}/resume")).StatusCode);
        var recruiterApps = await recruiter.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}/applications");
        Assert.Equal(0, recruiterApps!.GetProperty("total").GetInt32());
        var pendingState = await candidate.GetFromJsonAsync<JsonElement>("/api/account/deletion-request");
        Assert.Equal("pending", pendingState!.GetProperty("status").GetString());

        using var wrongCancel = await candidate.PostAsJsonAsync("/api/account/deletion-request/cancel", new { password = "wrong-password" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCancel.StatusCode);
        using var cancel = await candidate.PostAsJsonAsync("/api/account/deletion-request/cancel", new { password = Password });
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync("/api/profile/me")).StatusCode);
        using (var publicSelection = await candidate.GetAsync("/api/resumes/public/me"))
            Assert.Equal(HttpStatusCode.OK, publicSelection.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await recruiter.GetAsync($"/api/applications/{applicationId}/resume")).StatusCode);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal("active", (await db.Users.SingleAsync(x => x.Id == candidateId)).AccountStatus);
            var cancelled = await db.AccountDeletionRequests.SingleAsync(x => x.UserId == candidateId);
            Assert.Equal("cancelled", cancelled.Status);
            Assert.Null(cancelled.DataPurgedAt);
            Assert.Null(await db.CandidatePublicResumes.SingleOrDefaultAsync(x => x.CandidateId == candidateId));
            Assert.Equal(1, await db.Applications.CountAsync(x => x.Id == applicationId));
        }
    }

    [Fact]
    public async Task CandidatePurgeRemovesSnapshotsAndRetriesStorageWithoutFalseCompletion()
    {
        var (candidate, candidateId) = await fixture.User(password: Password);
        var (otherCandidate, otherCandidateId) = await fixture.User(password: ActiveCandidatePassword);
        var (recruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate, FoundationFixture.Docx("Synthetic purge fixture CV."));
        var versionId = upload.GetProperty("versionId").GetGuid();
        var extraction = await fixture.Enqueue(candidateId, versionId);
        await ProcessJob(extraction.Id);
        var publicResult = await candidate.PostAsJsonAsync("/api/resumes/public", new { resumeVersionId = versionId, expectedRowVersion = (long?)null });
        Assert.Equal(HttpStatusCode.OK, publicResult.StatusCode);
        var selection = await publicResult.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ready", selection.GetProperty("status").GetString());
        var (jobId, _) = await CreatePublishedJob(recruiter, "Candidate purge preserved job");
        using var apply = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId = versionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
        var applicationId = (await apply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var note = await recruiter.PostAsJsonAsync($"/api/applications/{applicationId}/notes", new { body = "Synthetic internal note to erase." });
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);

        string[] objectKeys;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            objectKeys = await db.StoredFiles.Where(x => x.OwnerUserId == candidateId).Select(x => x.ObjectKey).ToArrayAsync();
            Assert.Equal(1, await db.ResumeDataRevisions.CountAsync(x => x.CandidateId == candidateId));
        }
        Assert.Equal(2, objectKeys.Length);
        using var request = await candidate.PostAsJsonAsync("/api/account/deletion-request", new { password = Password });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        fixture.StorageFault.FailDeletes = true;
        fixture.Clock.Advance(TimeSpan.FromDays(7));
        Assert.True(await ProcessDeletionOnce("deletion-worker-a"));

        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var ledger = await db.AccountDeletionRequests.SingleAsync(x => x.UserId == candidateId);
            Assert.Equal("pending", ledger.Status);
            Assert.True(ledger.DataPurgedAt.HasValue, $"database purge failed: {ledger.LastErrorCode}");
            Assert.Null(ledger.PurgedAt);
            Assert.Equal("storage_delete_failed", ledger.LastErrorCode);
            Assert.Equal(2, await db.AccountDeletionObjects.CountAsync(x => x.RequestId == ledger.Id && x.Status == "pending"));
            Assert.Equal(0, await db.StoredFiles.CountAsync(x => x.OwnerUserId == candidateId));
            Assert.Equal(0, await db.ResumeVersions.CountAsync(x => x.CandidateId == candidateId));
            Assert.Equal(0, await db.ResumeDataRevisions.CountAsync(x => x.CandidateId == candidateId));
            Assert.Equal(0, await db.Applications.CountAsync(x => x.Id == applicationId));
            Assert.Equal(0, await db.ApplicationStatusHistory.CountAsync(x => x.ApplicationId == applicationId));
            Assert.Equal(0, await db.ApplicationNotes.CountAsync(x => x.ApplicationId == applicationId));
            var account = await db.Users.SingleAsync(x => x.Id == candidateId);
            Assert.Equal("deleted", account.AccountStatus);
            Assert.EndsWith("@deleted.invalid", account.Email);
            Assert.Null(account.PasswordHash);
            var profile = await db.CandidateProfiles.SingleAsync(x => x.UserId == candidateId);
            Assert.Equal("Ứng viên đã xóa", profile.DisplayName);
            Assert.Null(profile.ContactEmail);
            Assert.Equal("{}", profile.Details);
            Assert.Equal(1, await db.Jobs.CountAsync(x => x.Id == jobId));
        }
        using (var scope = fixture.App.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<CareerGap.Api.Files.IFileStorage>();
            Assert.NotEmpty(await storage.ReadAsync(objectKeys[0], default));
        }

        fixture.StorageFault.FailDeletes = false;
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.True(await ProcessDeletionOnce("deletion-worker-b"));
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var ledger = await db.AccountDeletionRequests.SingleAsync(x => x.UserId == candidateId);
            Assert.Equal("purged", ledger.Status);
            Assert.NotNull(ledger.PurgedAt);
            Assert.All(await db.AccountDeletionObjects.Where(x => x.RequestId == ledger.Id).ToArrayAsync(), x => Assert.Equal("deleted", x.Status));
            Assert.Equal(0, await db.Notifications.CountAsync(x => x.RecipientUserId == candidateId || x.ApplicationId == applicationId));
            Assert.Equal(1, await db.Jobs.CountAsync(x => x.Id == jobId));
        }
        using (var scope = fixture.App.Services.CreateScope())
        {
            var storage = scope.ServiceProvider.GetRequiredService<CareerGap.Api.Files.IFileStorage>();
            await Assert.ThrowsAsync<FileNotFoundException>(() => storage.ReadAsync(objectKeys[0], default));
        }
        using var anonymous = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/jobs/{jobId}")).StatusCode);
        await Reauthenticate(otherCandidate, await ReadUserEmail(otherCandidateId), ActiveCandidatePassword);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCandidate.GetAsync($"/api/public/resumes/{candidateId}/{versionId}/file")).StatusCode);
    }

    [Fact]
    public async Task RecruiterPurgeAnonymizesOwnerWithoutDeletingApplicantsOrJobSnapshots()
    {
        var (recruiter, recruiterId) = await fixture.User("Recruiter", Password);
        var (candidate, candidateId) = await fixture.User(password: ActiveCandidatePassword);
        var upload = await FoundationFixture.Upload(candidate, FoundationFixture.Docx("Synthetic CV stays with applicant."));
        var versionId = upload.GetProperty("versionId").GetGuid();
        var (jobId, _) = await CreatePublishedJob(recruiter, "Synthetic historical job");
        using var apply = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId = versionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
        var applicationId = (await apply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var reviewed = await recruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 0L, status = "reviewing" });
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        using var note = await recruiter.PostAsJsonAsync($"/api/applications/{applicationId}/notes", new { body = "Recruiter private note to remove." });
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);

        using var request = await recruiter.PostAsJsonAsync("/api/account/deletion-request", new { password = Password });
        Assert.Equal(HttpStatusCode.Accepted, request.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.App.CreateClient().GetAsync($"/api/jobs/{jobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync($"/api/applications/{applicationId}/resume")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await recruiter.GetAsync("/api/companies/mine")).StatusCode);

        fixture.Clock.Advance(TimeSpan.FromDays(7));
        Assert.True(await ProcessDeletionOnce("recruiter-purge-worker"));
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var account = await db.Users.SingleAsync(x => x.Id == recruiterId);
            Assert.Equal("deleted", account.AccountStatus);
            Assert.EndsWith("@deleted.invalid", account.Email);
            Assert.Empty(await db.UserRoles.Where(x => x.UserId == recruiterId).ToArrayAsync());
            var company = await db.Companies.SingleAsync(x => x.OwnerUserId == recruiterId);
            Assert.Equal("Nhà tuyển dụng đã xóa", company.Name);
            Assert.NotNull(company.ArchivedAt);
            Assert.Null(company.WebsiteUrl);
            var job = await db.Jobs.SingleAsync(x => x.Id == jobId);
            Assert.Equal("closed", job.Status);
            Assert.Equal(1, await db.Applications.CountAsync(x => x.Id == applicationId && x.CandidateId == candidateId));
            Assert.True(await db.ApplicationStatusHistory.AnyAsync(x => x.ApplicationId == applicationId && x.ActorUserId == recruiterId));
            Assert.Equal(0, await db.ApplicationNotes.CountAsync(x => x.ApplicationId == applicationId && x.AuthorUserId == recruiterId));
            Assert.Equal("active", (await db.Users.SingleAsync(x => x.Id == candidateId)).AccountStatus);
        }
        await Reauthenticate(candidate, await ReadUserEmail(candidateId), ActiveCandidatePassword);
        var candidateHistory = await candidate.GetFromJsonAsync<JsonElement[]>($"/api/applications/{applicationId}/history");
        Assert.Equal(2, candidateHistory!.Length);
        var myApps = await candidate.GetFromJsonAsync<JsonElement>("/api/applications/mine");
        Assert.Equal("Nhà tuyển dụng đã xóa", myApps!.GetProperty("items")[0].GetProperty("companyName").GetString());
    }

    [Fact]
    public async Task RestoreReplayPurgesAnOlderSnapshotBeforeItCanBeOpened()
    {
        var (candidate, candidateId) = await fixture.User(password: Password);
        var upload = await FoundationFixture.Upload(candidate, FoundationFixture.Docx("Synthetic recovery ledger CV."));
        var versionId = upload.GetProperty("versionId").GetGuid();
        var fileId = upload.GetProperty("fileId").GetGuid();
        using var response = await candidate.PostAsJsonAsync("/api/account/deletion-request", new { password = Password });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        AccountDeletionLedgerEntry ledgerEntry;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var deletion = await db.AccountDeletionRequests.SingleAsync(x => x.UserId == candidateId);
            ledgerEntry = new(deletion.Id, deletion.UserId, "purged", deletion.RequestedAt, deletion.PurgeAfter,
                null, deletion.PurgeAfter, deletion.PurgeAfter.AddMinutes(1));

            // Model a database snapshot made before T0: account and CV remain, but its separate
            // deletion ledger must erase that recovered copy before an API can be opened.
            await db.AccountDeletionRequests.Where(x => x.Id == deletion.Id).ExecuteDeleteAsync();
            await db.Users.Where(x => x.Id == candidateId).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.AccountStatus, "active")
                .SetProperty(x => x.SecurityStamp, Guid.NewGuid().ToString("N")));
        }
        fixture.Clock.Advance(TimeSpan.FromDays(7));

        using (var scope = fixture.App.Services.CreateScope())
        {
            var deletions = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
            await deletions.ReplayLedgerAsync([ledgerEntry], default);
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal("deleted", (await db.Users.SingleAsync(x => x.Id == candidateId)).AccountStatus);
            Assert.Null(await db.ResumeVersions.SingleOrDefaultAsync(x => x.Id == versionId));
            Assert.Equal("purged", (await db.AccountDeletionRequests.SingleAsync(x => x.Id == ledgerEntry.RequestId)).Status);
            Assert.Equal(0, await db.AccountDeletionObjects.CountAsync(x => x.RequestId == ledgerEntry.RequestId && x.Status == "pending"));
            var storage = scope.ServiceProvider.GetRequiredService<CareerGap.Api.Files.IFileStorage>();
            await Assert.ThrowsAsync<FileNotFoundException>(() => storage.ReadAsync(fileId.ToString("N"), default));
        }
    }

    [Fact]
    public async Task RestoreReplaySkipsLedgerEntriesForAccountsCreatedAfterTheSnapshot()
    {
        var now = fixture.Clock.GetUtcNow();
        var entry = new AccountDeletionLedgerEntry(Guid.NewGuid(), Guid.NewGuid(), "pending", now,
            now.AddDays(7), null, null, null);
        using var scope = fixture.App.Services.CreateScope();
        var deletions = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
        await deletions.ReplayLedgerAsync([entry], default);
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Null(await db.AccountDeletionRequests.SingleOrDefaultAsync(x => x.Id == entry.RequestId));
    }

    private async Task ProcessJob(Guid jobId)
    {
        using var scope = fixture.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<JobService>();
        AiJob? claimed;
        do
        {
            claimed = await service.Claim("revision-test-worker", default);
            Assert.NotNull(claimed);
            await JobWorker.Process(scope.ServiceProvider, claimed.Id, "revision-test-worker", claimed.LeaseGeneration, default);
        } while (claimed.Id != jobId);
    }

    private async Task<bool> ProcessDeletionOnce(string worker)
    {
        using var scope = fixture.App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().ProcessOneDueAsync(worker, default);
    }

    private async Task<string> ReadUserEmail(Guid userId)
    {
        using var scope = fixture.App.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDb>().Users.SingleAsync(x => x.Id == userId)).Email!;
    }

    private static async Task Reauthenticate(HttpClient client, string email, string password)
    {
        await FoundationFixture.Csrf(client);
        using var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await FoundationFixture.Csrf(client);
    }

    private static async Task<(Guid JobId, Guid CompanyId)> CreatePublishedJob(HttpClient recruiter, string title)
    {
        using var companyResponse = await recruiter.PostAsJsonAsync("/api/companies", new { name = $"Synthetic {Guid.NewGuid():N}"[..20], websiteUrl = "", location = "Hà Nội" });
        Assert.Equal(HttpStatusCode.Created, companyResponse.StatusCode);
        var company = await companyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var content = new
        {
            title, description = "Mô tả giả phục vụ kiểm thử.", requirementsText = "Yêu cầu giả.", benefitsText = "Quyền lợi giả.",
            industry = "Công nghệ", location = "Hà Nội", employmentType = "full_time", workMode = "remote",
            experienceMin = 1m, experienceMax = 5m, salaryMin = (decimal?)null, salaryMax = (decimal?)null,
            salaryCurrency = (string?)null, salaryPeriod = (string?)null, language = "vi"
        };
        using var created = await recruiter.PostAsJsonAsync("/api/jobs", new
        {
            companyId = company!.GetProperty("id").GetGuid(),
            applicationDeadline = DateTimeOffset.UtcNow.AddDays(30), content
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = draft!.GetProperty("id").GetGuid();
        using var publish = await recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/publish", new { rowVersion = draft.GetProperty("rowVersion").GetInt64() });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        return (jobId, company.GetProperty("id").GetGuid());
    }
}
