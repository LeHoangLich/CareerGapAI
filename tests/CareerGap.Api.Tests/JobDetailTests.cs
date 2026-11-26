using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Administration;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class JobDetailTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task PublicDetailsExcludePrivateModeratedAndInactiveSourcesWhileClosedAndExpiredJobsRemainReadable()
    {
        var (owner, ownerId) = await fixture.User("Recruiter");
        var (admin, _) = await fixture.User("Admin");
        var guest = fixture.App.CreateClient();
        var company = await Company(owner);
        var draft = await Job(owner, company, "F13 private draft");
        var id = draft.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/jobs/{id}")).StatusCode);
        var published = await Publish(owner, draft);
        var visible = await guest.GetFromJsonAsync<JsonElement>($"/api/jobs/{id}");
        Assert.True(visible.GetProperty("acceptingApplications").GetBoolean());
        Assert.Equal("F13 private draft", visible.GetProperty("version").GetProperty("title").GetString());
        Assert.Equal(1200.25m, visible.GetProperty("version").GetProperty("salaryMin").GetDecimal());
        Assert.Equal("https://example.invalid", visible.GetProperty("company").GetProperty("websiteUrl").GetString());
        Assert.Equal("Mô tả công việc.\nDòng thứ hai.", visible.GetProperty("version").GetProperty("description").GetString());
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Users.Where(x => x.Id == ownerId).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "suspended"));
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/jobs/{id}")).StatusCode);
        await db.Users.Where(x => x.Id == ownerId).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "active"));
        await db.Companies.Where(x => x.Id == company).ExecuteUpdateAsync(s => s.SetProperty(x => x.ArchivedAt, DateTimeOffset.UtcNow));
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/jobs/{id}")).StatusCode);
        await db.Companies.Where(x => x.Id == company).ExecuteUpdateAsync(s => s.SetProperty(x => x.ArchivedAt, (DateTimeOffset?)null));
        await db.Jobs.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ApplicationDeadline, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var expired = await guest.GetFromJsonAsync<JsonElement>($"/api/jobs/{id}");
        Assert.False(expired.GetProperty("acceptingApplications").GetBoolean());
        Assert.Equal("published", expired.GetProperty("status").GetString());
        var closedResponse = await owner.PostAsJsonAsync($"/api/jobs/{id}/close", new { rowVersion = published.GetProperty("rowVersion").GetInt64() });
        Assert.Equal(HttpStatusCode.OK, closedResponse.StatusCode);
        var closed = await closedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("closed", (await guest.GetFromJsonAsync<JsonElement>($"/api/jobs/{id}")).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/jobs/{id}", new AdminAction("archive", "F13 synthetic moderation", closed.GetProperty("rowVersion").GetInt64()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/jobs/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/jobs/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task CandidateJobContextFiltersBeforePagingAndNeverIncludesAnotherCandidatesSavedJobsOrApplications()
    {
        var (owner, _) = await fixture.User("Recruiter");
        var (candidate, candidateId) = await fixture.User();
        var (other, _) = await fixture.User();
        var company = await Company(owner);
        var resume = await FoundationFixture.Upload(candidate);
        var tied = DateTimeOffset.UtcNow;
        Guid target = default, targetApplication = default;
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        for (var index = 0; index < 55; index++)
        {
            var job = await Publish(owner, await Job(owner, company, "F13 context " + index));
            var id = job.GetProperty("id").GetGuid();
            var submittedAt = index == 0 ? tied.AddDays(-1) : tied;
            var application = new ApplicationRecord {
                CandidateId = candidateId, JobId = id, SubmittedResumeVersionId = resume.GetProperty("versionId").GetGuid(),
                SubmittedJobVersionId = job.GetProperty("version").GetProperty("id").GetGuid(), SubmittedAt = submittedAt,
                StatusChangedAt = submittedAt, SubmissionKey = Guid.NewGuid(), SubmissionHash = new string('a', 64)
            };
            db.Applications.Add(application);
            db.SavedJobs.Add(new SavedJob { CandidateId = candidateId, JobId = id, CreatedAt = submittedAt });
            if (index == 0) { target = id; targetApplication = application.Id; }
        }
        await db.SaveChangesAsync();
        var first = await candidate.GetFromJsonAsync<JsonElement>("/api/applications/mine?pageSize=50");
        Assert.Equal(55, first.GetProperty("total").GetInt32());
        Assert.DoesNotContain(first.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == targetApplication);
        var savedFirst = await candidate.GetFromJsonAsync<JsonElement>("/api/saved-jobs?pageSize=50");
        Assert.DoesNotContain(savedFirst.GetProperty("items").EnumerateArray(), item => item.GetProperty("job").GetProperty("id").GetGuid() == target);
        var specific = await candidate.GetFromJsonAsync<JsonElement>($"/api/applications/mine?jobId={target}&pageSize=1");
        Assert.Equal(1, specific.GetProperty("total").GetInt32());
        Assert.Equal(targetApplication, Assert.Single(specific.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        var savedSpecific = await candidate.GetFromJsonAsync<JsonElement>($"/api/saved-jobs?jobId={target}&pageSize=1");
        Assert.Equal(1, savedSpecific.GetProperty("total").GetInt32());
        Assert.Equal(target, Assert.Single(savedSpecific.GetProperty("items").EnumerateArray()).GetProperty("job").GetProperty("id").GetGuid());
        foreach (var path in new[] { "/api/applications/mine", "/api/saved-jobs" })
        {
            Assert.Equal(0, (await other.GetFromJsonAsync<JsonElement>($"{path}?jobId={target}")).GetProperty("total").GetInt32());
            Assert.Equal(0, (await candidate.GetFromJsonAsync<JsonElement>($"{path}?jobId={Guid.NewGuid()}")).GetProperty("total").GetInt32());
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.App.CreateClient().GetAsync($"{path}?jobId={target}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"{path}?jobId={target}")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await candidate.GetAsync($"{path}?jobId=invalid")).StatusCode);
        }
    }

    private async Task<Guid> Company(HttpClient owner)
    {
        var response = await owner.PostAsJsonAsync("/api/companies", new { name = "F13 synthetic company", description = "Test company only", websiteUrl = "https://example.invalid", location = "Hà Nội" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private async Task<JsonElement> Job(HttpClient owner, Guid companyId, string title)
    {
        var response = await owner.PostAsJsonAsync("/api/jobs", new { companyId, content = new {
            title, description = "Mô tả công việc.\nDòng thứ hai.", requirementsText = "React và PostgreSQL", benefitsText = "Dữ liệu tổng hợp",
            employmentType = "full_time", workMode = "remote", salaryMin = 1200.25m, salaryMax = 2400.5m, salaryCurrency = "USD", salaryPeriod = "month", language = "vi"
        }});
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private async Task<JsonElement> Publish(HttpClient owner, JsonElement job)
    {
        var response = await fixture.PublishWithVerifiedBusinessFixture(owner, job.GetProperty("id").GetGuid(), new { rowVersion = job.GetProperty("rowVersion").GetInt64() });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
