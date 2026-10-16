using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Applications;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class ApplicationTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task ScreeningTransitionsNotesAndCandidateWithdrawalRespectRolesAndHistory()
    {
        var (candidate, candidateId) = await fixture.User();
        var (recruiter, recruiterId) = await fixture.User("Recruiter");
        var (otherRecruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate);
        var resumeVersionId = upload.GetProperty("versionId").GetGuid();
        var (jobId, _, _) = await CreatePublishedJob(recruiter, "Screening workflow");
        using var submit = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var first = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var applicationId = first!.GetProperty("id").GetGuid();

        using var candidateStatusChange = await candidate.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 0, status = "reviewing" });
        Assert.Equal(HttpStatusCode.Forbidden, candidateStatusChange.StatusCode);
        using var wrongOwner = await otherRecruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 0, status = "reviewing" });
        Assert.Equal(HttpStatusCode.NotFound, wrongOwner.StatusCode);
        using var reviewingResponse = await recruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 0, status = "reviewing" });
        Assert.Equal(HttpStatusCode.OK, reviewingResponse.StatusCode);
        var reviewing = await reviewingResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("reviewing", reviewing!.GetProperty("status").GetString());
        Assert.Equal(1, reviewing.GetProperty("rowVersion").GetInt64());

        using var stale = await recruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 0, status = "shortlisted" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalidTransition = await recruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 1, status = "submitted" });
        Assert.Equal(HttpStatusCode.Conflict, invalidTransition.StatusCode);
        using var shortlistedResponse = await recruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 1, status = "shortlisted" });
        Assert.Equal(HttpStatusCode.OK, shortlistedResponse.StatusCode);
        using var reviewingAgain = await recruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 2, status = "reviewing" });
        Assert.Equal(HttpStatusCode.OK, reviewingAgain.StatusCode);
        using var rejectedResponse = await recruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 3, status = "rejected" });
        Assert.Equal(HttpStatusCode.OK, rejectedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await recruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 4, status = "reviewing" })).StatusCode);

        const string privateNote = "Nội bộ: cần xác minh kinh nghiệm giả lập.";
        using var noteResponse = await recruiter.PostAsJsonAsync($"/api/applications/{applicationId}/notes", new { body = $"  {privateNote}  " });
        Assert.Equal(HttpStatusCode.Created, noteResponse.StatusCode);
        var note = await noteResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(privateNote, note!.GetProperty("body").GetString());
        Assert.Equal(HttpStatusCode.OK, (await recruiter.GetAsync($"/api/applications/{applicationId}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.GetAsync($"/api/applications/{applicationId}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.GetAsync($"/api/applications/{applicationId}/notes")).StatusCode);

        var history = await candidate.GetFromJsonAsync<JsonElement[]>($"/api/applications/{applicationId}/history");
        Assert.Equal(5, history!.Length);
        Assert.Equal("submitted", history[0].GetProperty("toStatus").GetString());
        Assert.Equal("rejected", history[^1].GetProperty("toStatus").GetString());
        var candidateApplications = await candidate.GetAsync("/api/applications/mine");
        var candidateApplicationJson = await candidateApplications.Content.ReadAsStringAsync();
        Assert.DoesNotContain(privateNote, candidateApplicationJson, StringComparison.Ordinal);
        var candidateNotices = await candidate.GetFromJsonAsync<JsonElement[]>("/api/notifications");
        Assert.DoesNotContain(candidateNotices!, notification => notification.GetProperty("body").GetString()!.Contains(privateNote, StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Conflict, (await candidate.PostAsJsonAsync($"/api/applications/{applicationId}/withdraw", new { rowVersion = 4L })).StatusCode);

        using var reapply = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, reapply.StatusCode);
        var second = await reapply.Content.ReadFromJsonAsync<JsonElement>();
        var secondId = second!.GetProperty("id").GetGuid();
        Assert.NotEqual(applicationId, secondId);
        using var withdrawn = await candidate.PostAsJsonAsync($"/api/applications/{secondId}/withdraw", new { rowVersion = 0L });
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        Assert.Equal("withdrawn", (await withdrawn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        using var thirdSubmit = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, thirdSubmit.StatusCode);
        var thirdId = (await thirdSubmit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PutAsJsonAsync($"/api/applications/{thirdId}/status", new { rowVersion = 0L, status = "shortlisted" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PutAsJsonAsync($"/api/applications/{thirdId}/status", new { rowVersion = 1L, status = "rejected" })).StatusCode);

        using var fourthSubmit = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, fourthSubmit.StatusCode);
        var fourthId = (await fourthSubmit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PutAsJsonAsync($"/api/applications/{fourthId}/status", new { rowVersion = 0L, status = "rejected" })).StatusCode);

        using var fifthSubmit = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, fifthSubmit.StatusCode);
        var fifthId = (await fifthSubmit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PutAsJsonAsync($"/api/applications/{fifthId}/status", new { rowVersion = 0L, status = "reviewing" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync($"/api/applications/{fifthId}/withdraw", new { rowVersion = 1L })).StatusCode);

        using var sixthSubmit = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, sixthSubmit.StatusCode);
        var sixthId = (await sixthSubmit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PutAsJsonAsync($"/api/applications/{sixthId}/status", new { rowVersion = 0L, status = "shortlisted" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync($"/api/applications/{sixthId}/withdraw", new { rowVersion = 1L })).StatusCode);

        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal("rejected", (await db.Applications.SingleAsync(x => x.Id == applicationId)).Status);
            Assert.Equal("withdrawn", (await db.Applications.SingleAsync(x => x.Id == secondId)).Status);
            Assert.Equal("rejected", (await db.Applications.SingleAsync(x => x.Id == thirdId)).Status);
            Assert.Equal("rejected", (await db.Applications.SingleAsync(x => x.Id == fourthId)).Status);
            Assert.Equal("withdrawn", (await db.Applications.SingleAsync(x => x.Id == fifthId)).Status);
            Assert.Equal("withdrawn", (await db.Applications.SingleAsync(x => x.Id == sixthId)).Status);
            Assert.Equal(5, await db.ApplicationStatusHistory.CountAsync(x => x.ApplicationId == applicationId));
            Assert.Equal(1, await db.ApplicationNotes.CountAsync(x => x.ApplicationId == applicationId && x.AuthorUserId == recruiterId));
            var immutableNote = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE application_notes SET body = 'edited' WHERE application_id = {applicationId}"));
            Assert.Equal("23514", immutableNote.SqlState);
            Assert.Equal(5, await db.Notifications.CountAsync(x => x.ApplicationId == applicationId && x.RecipientUserId == candidateId));
        }
    }

    [Fact]
    public async Task ExpiredTerminalApplicationIsHiddenAtNinetyDaysThenPurgedWithoutDeletingOwnedCv()
    {
        var (candidate, candidateId) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate);
        var resumeVersionId = upload.GetProperty("versionId").GetGuid();
        var fileId = upload.GetProperty("fileId").GetGuid();
        var (jobId, _, _) = await CreatePublishedJob(recruiter, "Retention boundary");
        using var submit = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var applicationId = (await submit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var rejected = await recruiter.PutAsJsonAsync($"/api/applications/{applicationId}/status", new { rowVersion = 0L, status = "rejected" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        using var note = await recruiter.PostAsJsonAsync($"/api/applications/{applicationId}/notes", new { body = "Synthetic retention-only note." });
        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.Applications.Where(x => x.Id == applicationId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StatusChangedAt, fixture.Clock.GetUtcNow().AddDays(-90)));
        }

        var candidateApps = await candidate.GetFromJsonAsync<JsonElement>("/api/applications/mine");
        Assert.Equal(0, candidateApps!.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await candidate.GetAsync($"/api/applications/{applicationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await candidate.GetAsync($"/api/applications/{applicationId}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/api/applications/{applicationId}/resume")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/api/applications/{applicationId}/notes")).StatusCode);
        Assert.Equal(0, (await recruiter.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}/applications"))!.GetProperty("total").GetInt32());
        Assert.Empty(await candidate.GetFromJsonAsync<JsonElement[]>("/api/notifications") ?? []);

        using (var scope = fixture.App.Services.CreateScope())
        {
            var retention = scope.ServiceProvider.GetRequiredService<ApplicationRetentionService>();
            Assert.True(await retention.PurgeExpiredAsync(default) >= 1);
        }
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Null(await db.Applications.SingleOrDefaultAsync(x => x.Id == applicationId));
            Assert.Equal(0, await db.ApplicationStatusHistory.CountAsync(x => x.ApplicationId == applicationId));
            Assert.Equal(0, await db.ApplicationNotes.CountAsync(x => x.ApplicationId == applicationId));
            Assert.Equal(0, await db.Notifications.CountAsync(x => x.ApplicationId == applicationId));
            Assert.NotNull(await db.ResumeVersions.SingleOrDefaultAsync(x => x.Id == resumeVersionId && x.CandidateId == candidateId));
        }
        Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync($"/api/files/{fileId}")).StatusCode);
    }

    [Fact]
    public async Task JobCandidateBlockIsOwnerScopedDoesNotRevokeExistingApplicationsAndAllowsReapplyAfterUnblock()
    {
        var (candidate, candidateId) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var (otherRecruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate);
        var resumeVersionId = upload.GetProperty("versionId").GetGuid();
        var (blockedJobId, _, _) = await CreatePublishedJob(recruiter, "Blocked job");
        var (otherJobId, _, _) = await CreatePublishedJob(recruiter, "Other job");
        using var initial = await candidate.PostAsJsonAsync($"/api/jobs/{blockedJobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, initial.StatusCode);
        var originalApplicationId = (await initial.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.PutAsync($"/api/jobs/{blockedJobId}/candidates/{candidateId}/block", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.PutAsync($"/api/jobs/{blockedJobId}/candidates/{candidateId}/block", null)).StatusCode);
        var blockPath = $"/api/jobs/{blockedJobId}/candidates/{candidateId}/block";
        Assert.Equal(HttpStatusCode.NoContent, (await recruiter.PutAsync(blockPath, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await recruiter.PutAsync(blockPath, null)).StatusCode);
        var recruiterPage = await recruiter.GetFromJsonAsync<JsonElement>($"/api/jobs/{blockedJobId}/applications");
        Assert.True(recruiterPage!.GetProperty("items")[0].GetProperty("candidateBlocked").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await recruiter.GetAsync($"/api/applications/{originalApplicationId}/resume")).StatusCode);
        var candidatePage = await candidate.GetFromJsonAsync<JsonElement>("/api/applications/mine");
        Assert.False(candidatePage!.GetProperty("items")[0].GetProperty("candidateBlocked").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync($"/api/applications/{originalApplicationId}/withdraw", new { rowVersion = 0L })).StatusCode);
        using var blockedReapply = await candidate.PostAsJsonAsync($"/api/jobs/{blockedJobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, blockedReapply.StatusCode);
        Assert.Contains("candidate_blocked", await blockedReapply.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var otherJobApply = await candidate.PostAsJsonAsync($"/api/jobs/{otherJobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, otherJobApply.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await recruiter.DeleteAsync(blockPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await recruiter.DeleteAsync(blockPath)).StatusCode);
        using var allowedReapply = await candidate.PostAsJsonAsync($"/api/jobs/{blockedJobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Created, allowedReapply.StatusCode);
        Assert.NotEqual(originalApplicationId, (await allowedReapply.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task BlockAndSubmissionSerializeOnTheJobRow()
    {
        var (candidate, candidateId) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate);
        var resumeVersionId = upload.GetProperty("versionId").GetGuid();
        var (jobId, _, _) = await CreatePublishedJob(recruiter, "Block apply race");

        var submitTask = candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        var blockTask = recruiter.PutAsync($"/api/jobs/{jobId}/candidates/{candidateId}/block", null);
        var responses = await Task.WhenAll(submitTask, blockTask);
        using var submit = responses[0];
        using var block = responses[1];
        Assert.Equal(HttpStatusCode.NoContent, block.StatusCode);
        Assert.Contains(submit.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        if (submit.StatusCode == HttpStatusCode.Conflict)
            Assert.Contains("candidate_blocked", await submit.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var afterRace = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, afterRace.StatusCode);
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var applications = await db.Applications.Where(x => x.JobId == jobId && x.CandidateId == candidateId).ToListAsync();
        Assert.Equal(submit.StatusCode == HttpStatusCode.Created ? 1 : 0, applications.Count);
        if (applications.Count == 1) Assert.Equal("submitted", applications[0].Status);
    }

    [Fact]
    public async Task SubmissionPinsVersionsIsIdempotentAndRestrictsSnapshotFileToApplicationContext()
    {
        var (candidate, candidateId) = await fixture.User();
        var (otherCandidate, _) = await fixture.User();
        var (recruiter, recruiterId) = await fixture.User("Recruiter");
        var (otherRecruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate, FoundationFixture.Docx("Synthetic resume version one"), "private-v1.docx");
        var resumeVersionId = upload.GetProperty("versionId").GetGuid();
        var fileId = upload.GetProperty("fileId").GetGuid();
        var (jobId, jobVersionId, rowVersion) = await CreatePublishedJob(recruiter, "Platform Engineer snapshot");
        var key = Guid.NewGuid();

        using var forbiddenRole = await recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRole.StatusCode);
        using var missingResume = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId = Guid.NewGuid(), submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, missingResume.StatusCode);

        using var submitted = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = key });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var created = await submitted.Content.ReadFromJsonAsync<JsonElement>();
        var applicationId = created.GetProperty("id").GetGuid();
        Assert.Equal(jobId, created.GetProperty("jobId").GetGuid());
        Assert.Equal("Platform Engineer snapshot", created.GetProperty("jobTitle").GetString());
        Assert.Equal("submitted", created.GetProperty("status").GetString());
        Assert.Equal(1, created.GetProperty("resumeVersionNo").GetInt32());

        using var replay = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = key });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(applicationId, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        using var duplicateActive = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, duplicateActive.StatusCode);

        var update = new
        {
            rowVersion,
            applicationDeadline = DateTimeOffset.UtcNow.AddDays(45),
            content = Content("Updated platform role")
        };
        using var updatedJob = await recruiter.PutAsJsonAsync($"/api/jobs/{jobId}", update);
        Assert.Equal(HttpStatusCode.OK, updatedJob.StatusCode);
        using var replayAfterEdit = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = key });
        Assert.Equal(HttpStatusCode.OK, replayAfterEdit.StatusCode);
        Assert.Equal("Platform Engineer snapshot", (await replayAfterEdit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("jobTitle").GetString());

        var mine = await candidate.GetFromJsonAsync<JsonElement>("/api/applications/mine?page=1&pageSize=10");
        Assert.Equal(1, mine!.GetProperty("total").GetInt32());
        Assert.Equal(applicationId, mine.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(0, (await otherCandidate.GetFromJsonAsync<JsonElement>("/api/applications/mine")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync("/api/applications/mine")).StatusCode);

        var recruiterPage = await recruiter.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}/applications");
        Assert.Equal(1, recruiterPage!.GetProperty("total").GetInt32());
        Assert.Equal("Platform Engineer snapshot", recruiterPage.GetProperty("items")[0].GetProperty("jobTitle").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.GetAsync($"/api/jobs/{jobId}/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.GetAsync($"/api/jobs/{jobId}/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await recruiter.GetAsync($"/api/applications/{applicationId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.GetAsync($"/api/applications/{applicationId}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync($"/api/files/{fileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/api/files/{fileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCandidate.GetAsync($"/api/applications/{applicationId}/resume")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.GetAsync($"/api/applications/{applicationId}/resume")).StatusCode);
        using var candidateSnapshot = await candidate.GetAsync($"/api/applications/{applicationId}/resume");
        Assert.Equal(HttpStatusCode.OK, candidateSnapshot.StatusCode);
        using var recruiterSnapshot = await recruiter.GetAsync($"/api/applications/{applicationId}/resume");
        Assert.Equal(HttpStatusCode.OK, recruiterSnapshot.StatusCode);
        Assert.Contains("Synthetic resume version one", await ReadDocxText(recruiterSnapshot));

        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var application = await db.Applications.SingleAsync(x => x.Id == applicationId);
            Assert.Equal(candidateId, application.CandidateId);
            Assert.Equal(jobVersionId, application.SubmittedJobVersionId);
            Assert.Equal(resumeVersionId, application.SubmittedResumeVersionId);
            Assert.Equal(1, await db.ApplicationStatusHistory.CountAsync(x => x.ApplicationId == applicationId));
            Assert.Equal(2, await db.Notifications.CountAsync(x => x.ApplicationId == applicationId));
            Assert.Equal(1, await db.Notifications.CountAsync(x => x.RecipientUserId == candidateId && x.ApplicationId == applicationId));
            Assert.Equal(1, await db.Notifications.CountAsync(x => x.RecipientUserId == recruiterId && x.ApplicationId == applicationId));

            var immutableSnapshot = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE applications SET submitted_resume_version_id = {Guid.NewGuid()} WHERE id = {applicationId}"));
            Assert.Equal("23514", immutableSnapshot.SqlState);
            var immutableHistory = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE application_status_history SET reason_code = 'tampered' WHERE application_id = {applicationId}"));
            Assert.Equal("23514", immutableHistory.SqlState);
        }

        var candidateNotices = await candidate.GetFromJsonAsync<JsonElement[]>("/api/notifications");
        var notice = Assert.Single(candidateNotices!);
        Assert.Equal(applicationId, notice.GetProperty("applicationId").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, (await candidate.PutAsync($"/api/notifications/{notice.GetProperty("id").GetGuid()}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherCandidate.PutAsync($"/api/notifications/{notice.GetProperty("id").GetGuid()}/read", null)).StatusCode);

        // Closing a job serializes with submission. A replay of the same key still returns its pinned snapshot.
        var currentJob = await recruiter.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}");
        var currentVersion = currentJob!.GetProperty("rowVersion").GetInt64();
        Assert.Equal(HttpStatusCode.OK, (await recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/close", new { rowVersion = currentVersion })).StatusCode);
        using var replayAfterClose = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = key });
        Assert.Equal(HttpStatusCode.OK, replayAfterClose.StatusCode);
        using var newKeyAfterClose = await candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Conflict, newKeyAfterClose.StatusCode);

        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE applications SET status = 'rejected', status_changed_at = {DateTimeOffset.UtcNow.AddDays(-91)} WHERE id = {applicationId}");
        }
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/api/applications/{applicationId}")).StatusCode);
        Assert.Equal(0, (await recruiter.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}/applications")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/api/applications/{applicationId}/resume")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await candidate.GetAsync($"/api/applications/{applicationId}/resume")).StatusCode);
    }

    [Fact]
    public async Task SubmissionAndCloseSerializeOnTheJobRow()
    {
        var (candidate, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate);
        var resumeVersionId = upload.GetProperty("versionId").GetGuid();
        var (jobId, _, rowVersion) = await CreatePublishedJob(recruiter, "Race test role");
        var key = Guid.NewGuid();

        var submissionTask = candidate.PostAsJsonAsync($"/api/jobs/{jobId}/applications", new { resumeVersionId, submissionKey = key });
        var closeTask = recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/close", new { rowVersion });
        var responses = await Task.WhenAll(submissionTask, closeTask);
        using var submitResponse = responses[0];
        using var closeResponse = responses[1];
        var submitStatus = submitResponse.StatusCode;
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        Assert.Contains(submitStatus, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict });
        var job = await fixture.App.CreateClient().GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}");
        Assert.Equal("closed", job!.GetProperty("status").GetString());
        var applications = await candidate.GetFromJsonAsync<JsonElement>("/api/applications/mine");
        Assert.Equal(submitStatus == HttpStatusCode.Created ? 1 : 0, applications!.GetProperty("total").GetInt32());
    }

    private static async Task<(Guid JobId, Guid JobVersionId, long RowVersion)> CreatePublishedJob(HttpClient recruiter, string title)
    {
        using var companyResponse = await recruiter.PostAsJsonAsync("/api/companies", new { name = $"Synthetic {Guid.NewGuid():N}"[..20], websiteUrl = "", location = "Hà Nội" });
        Assert.Equal(HttpStatusCode.Created, companyResponse.StatusCode);
        var company = await companyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var content = Content(title);
        using var created = await recruiter.PostAsJsonAsync("/api/jobs", new
        {
            companyId = company!.GetProperty("id").GetGuid(),
            applicationDeadline = DateTimeOffset.UtcNow.AddDays(30),
            content
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = draft!.GetProperty("id").GetGuid();
        var versionId = draft.GetProperty("version").GetProperty("id").GetGuid();
        using var publish = await recruiter.PostAsJsonAsync($"/api/jobs/{jobId}/publish", new { rowVersion = draft.GetProperty("rowVersion").GetInt64() });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var published = await publish.Content.ReadFromJsonAsync<JsonElement>();
        return (jobId, versionId, published!.GetProperty("rowVersion").GetInt64());
    }

    private static object Content(string title) => new
    {
        title, description = "Mô tả giả phục vụ kiểm thử.", requirementsText = "Yêu cầu giả.", benefitsText = "Quyền lợi giả.",
        industry = "Công nghệ", location = "Hà Nội", employmentType = "full_time", workMode = "remote",
        experienceMin = 1m, experienceMax = 5m, salaryMin = (decimal?)null, salaryMax = (decimal?)null,
        salaryCurrency = (string?)null, salaryPeriod = (string?)null, language = "vi"
    };

    private static async Task<string> ReadDocxText(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var stream = new MemoryStream(bytes);
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, false);
        return string.Concat(document.MainDocumentPart!.Document!.Body!.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(x => x.Text));
    }
}
