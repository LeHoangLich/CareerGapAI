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

public sealed class AdminDashboardAndFilterTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task AdminAccountsAndJobsSupportFiltersAndMetadataAndLoginUpdatesLastLoginAt()
    {
        var (admin, _) = await fixture.User("Admin");
        var (candidate, candidateId) = await fixture.User("Candidate");
        var (recruiter, recruiterId) = await fixture.User("Recruiter", "RecruiterPass!123");

        // Trigger explicit login to verify LastLoginAt is populated.
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var email = await db.Users.Where(x => x.Id == recruiterId).Select(x => x.Email!).SingleAsync();
            await FoundationFixture.Csrf(recruiter);
            var login = await recruiter.PostAsJsonAsync("/api/auth/login", new { email, password = "RecruiterPass!123" });
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        }

        // Create company, verify it, publish a job with tags, and submit an application.
        var compResp = await recruiter.PostAsJsonAsync("/api/companies", new CompanyCreateRequest("AdminFilter Co", null, "https://example.com", "Hà Nội", "CNTT", "11-50"));
        Assert.Equal(HttpStatusCode.Created, compResp.StatusCode);
        var company = (await compResp.Content.ReadFromJsonAsync<CompanyDto>())!;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var identity = await db.BusinessIdentities.SingleAsync(x => x.UserId == recruiterId);
            identity.Status = "verified";
            await db.SaveChangesAsync();
        }
        var content = new JobContentRequest("Senior .NET Filter Test", "API", ".NET", null, "CNTT", "Hà Nội", "full_time", "remote", null, null, null, null, null, null, "vi");
        var createJob = await recruiter.PostAsJsonAsync("/api/jobs", new JobCreateRequest(company.Id, null, content, ["React", ".NET"]));
        var job = (await createJob.Content.ReadFromJsonAsync<JobDto>())!;
        await recruiter.PostAsJsonAsync($"/api/jobs/{job.Id}/publish", new JobStateRequest(job.RowVersion));

        var cv = await FoundationFixture.Upload(candidate);
        await candidate.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplyRequest(cv.GetProperty("versionId").GetGuid(), Guid.NewGuid()));

        // Filter accounts by role + status.
        var filteredRecruiters = await admin.GetFromJsonAsync<JsonElement>("/api/admin/accounts?status=active&role=Recruiter");
        var recruiterItem = Assert.Single(filteredRecruiters.EnumerateArray(), x => x.GetProperty("id").GetGuid() == recruiterId);
        Assert.True(recruiterItem.TryGetProperty("createdAt", out _));
        Assert.NotEqual(JsonValueKind.Null, recruiterItem.GetProperty("lastLoginAt").ValueKind);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/accounts?status=invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/accounts?role=SuperUser")).StatusCode);

        // Filter jobs by status + query.
        var filteredJobs = await admin.GetFromJsonAsync<JsonElement>("/api/admin/jobs?status=published&query=AdminFilter");
        var jobItem = Assert.Single(filteredJobs.EnumerateArray(), x => x.GetProperty("id").GetGuid() == job.Id);
        Assert.Equal(1, jobItem.GetProperty("applicationCount").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, jobItem.GetProperty("publishedAt").ValueKind);

        // Dashboard metrics & series.
        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.GetAsync("/api/admin/dashboard?days=7")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/dashboard?days=10")).StatusCode);

        var dash = await admin.GetFromJsonAsync<JsonElement>("/api/admin/dashboard?days=7");
        Assert.Equal(7, dash.GetProperty("days").GetInt32());
        Assert.True(dash.GetProperty("totals").GetProperty("users").GetProperty("total").GetInt32() >= 3);
        Assert.True(dash.GetProperty("totals").GetProperty("jobs").GetProperty("published").GetInt32() >= 1);
        Assert.True(dash.GetProperty("totals").GetProperty("applications").GetProperty("active").GetInt32() >= 1);
        Assert.Equal(7, dash.GetProperty("series").GetProperty("applicationsByDay").GetArrayLength());
        Assert.Equal(7, dash.GetProperty("series").GetProperty("jobsPublishedByDay").GetArrayLength());
        Assert.Contains(dash.GetProperty("series").GetProperty("topJobTags").EnumerateArray(), t => t.GetProperty("tag").GetString() == ".NET");
    }
}
