using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Applications;
using CareerGap.Api.Data;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class JobCapacityAndSoftDeleteTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private async Task<(HttpClient Recruiter, Guid CompanyId, JobDto Job)> CreatePublishedJob(int? maxApplications = null)
    {
        var (recruiter, recruiterId) = await fixture.User("Recruiter");
        var companyResponse = await recruiter.PostAsJsonAsync("/api/companies", new CompanyCreateRequest("Cap Co", null, "https://example.com", "Hà Nội", "CNTT", "11-50"));
        Assert.Equal(HttpStatusCode.Created, companyResponse.StatusCode);
        var company = (await companyResponse.Content.ReadFromJsonAsync<CompanyDto>())!;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var identity = await db.BusinessIdentities.SingleAsync(x => x.UserId == recruiterId);
            identity.Status = "verified";
            await db.SaveChangesAsync();
        }
        var content = new JobContentRequest("Backend Developer", "Build API", "C# and PostgreSQL", null, "CNTT", "Hà Nội", "full_time", "remote", null, null, null, null, null, null, "vi");
        var create = await recruiter.PostAsJsonAsync("/api/jobs", new JobCreateRequest(company.Id, null, content, null, maxApplications));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var job = (await create.Content.ReadFromJsonAsync<JobDto>())!;
        var publish = await recruiter.PostAsJsonAsync($"/api/jobs/{job.Id}/publish", new JobStateRequest(job.RowVersion));
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        job = (await publish.Content.ReadFromJsonAsync<JobDto>())!;
        return (recruiter, company.Id, job);
    }

    private async Task<(HttpClient Client, Guid ResumeVersionId)> CreateCandidateWithResume()
    {
        var (candidate, _) = await fixture.User("Candidate");
        var payload = await FoundationFixture.Upload(candidate);
        return (candidate, payload.GetProperty("versionId").GetGuid());
    }

    [Fact]
    public async Task MaxApplicationsEnforcesCapacityAndReleasesSlotOnWithdrawOrReject()
    {
        var (recruiter, _, job) = await CreatePublishedJob(maxApplications: 1);
        Assert.Equal(1, job.MaxApplications);
        Assert.Equal(0, job.ActiveApplicationCount);
        Assert.Equal(1, job.RemainingSlots);

        var (c1, r1) = await CreateCandidateWithResume();
        var (c2, r2) = await CreateCandidateWithResume();

        // Concurrent submissions for 1 slot: exactly one succeeds and one hits capacity.
        var t1 = c1.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplyRequest(r1, Guid.NewGuid()));
        var t2 = c2.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplyRequest(r2, Guid.NewGuid()));
        var responses = await Task.WhenAll(t1, t2);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);

        var winnerClient = responses[0].StatusCode == HttpStatusCode.Created ? c1 : c2;
        var winnerApp = (await (responses[0].StatusCode == HttpStatusCode.Created ? responses[0] : responses[1]).Content.ReadFromJsonAsync<ApplicationDto>())!;
        var loserClient = winnerClient == c1 ? c2 : c1;
        var loserResume = winnerClient == c1 ? r2 : r1;

        var fullJob = await fixture.App.CreateClient().GetFromJsonAsync<JobDto>($"/api/jobs/{job.Id}");
        Assert.NotNull(fullJob);
        Assert.False(fullJob!.AcceptingApplications);
        Assert.Equal(0, fullJob.RemainingSlots);

        // Withdrawing releases the slot so the second candidate can apply.
        var withdraw = await winnerClient.PostAsJsonAsync($"/api/applications/{winnerApp.Id}/withdraw", new { rowVersion = winnerApp.RowVersion });
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);

        var secondApply = await loserClient.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplyRequest(loserResume, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, secondApply.StatusCode);
        var secondApp = (await secondApply.Content.ReadFromJsonAsync<ApplicationDto>())!;

        // Rejecting also releases the slot.
        var reject = await recruiter.PutAsJsonAsync($"/api/applications/{secondApp.Id}/status", new { status = "rejected", rowVersion = secondApp.RowVersion });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);
        var reopenedJob = await fixture.App.CreateClient().GetFromJsonAsync<JobDto>($"/api/jobs/{job.Id}");
        Assert.True(reopenedJob!.AcceptingApplications);
        Assert.Equal(1, reopenedJob.RemainingSlots);
    }

    [Fact]
    public async Task RecruiterCanSoftDeleteAndRestoreOwnJobWithoutLosingApplicationsWhileAdminArchiveStaysLocked()
    {
        var (recruiter, _, job) = await CreatePublishedJob();
        var (candidate, resumeVersionId) = await CreateCandidateWithResume();
        var apply = await candidate.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplyRequest(resumeVersionId, Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Created, apply.StatusCode);
        var appDto = (await apply.Content.ReadFromJsonAsync<ApplicationDto>())!;

        // Recruiter soft-deletes (archives) the published job.
        var archive = await recruiter.PostAsJsonAsync($"/api/jobs/{job.Id}/archive", new JobStateRequest(job.RowVersion));
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
        var archived = (await archive.Content.ReadFromJsonAsync<JobDto>())!;
        Assert.Equal("archived", archived.Status);
        Assert.Equal("recruiter", archived.ArchivedByRole);

        // Public listing and detail no longer expose the archived job, while application snapshot remains accessible.
        var guest = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/jobs/{job.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.GetAsync($"/api/applications/{appDto.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await recruiter.GetAsync($"/api/applications/{appDto.Id}/resume")).StatusCode);

        // Recruiter restores own archived job -> returns as closed.
        var restore = await recruiter.PostAsJsonAsync($"/api/jobs/{job.Id}/restore", new JobStateRequest(archived.RowVersion));
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        var restored = (await restore.Content.ReadFromJsonAsync<JobDto>())!;
        Assert.Equal("closed", restored.Status);
        Assert.Null(restored.ArchivedByRole);

        // Admin archives the job -> recruiter cannot self-restore it.
        var (admin, _) = await fixture.User("Admin");
        var adminArchive = await admin.PostAsJsonAsync($"/api/admin/jobs/{job.Id}", new { action = "archive", reason = "Policy check", rowVersion = restored.RowVersion });
        Assert.Equal(HttpStatusCode.NoContent, adminArchive.StatusCode);

        var latestMine = await recruiter.GetFromJsonAsync<JobPage<JobDto>>("/api/jobs/mine?status=archived");
        var adminArchived = Assert.Single(latestMine!.Items, x => x.Id == job.Id);
        Assert.Equal("admin", adminArchived.ArchivedByRole);

        var forbiddenRestore = await recruiter.PostAsJsonAsync($"/api/jobs/{job.Id}/restore", new JobStateRequest(adminArchived.RowVersion));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRestore.StatusCode);
    }
}
