using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class CompanyJobTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task RecruiterOwnsCompanyAndVersionedJobWhileCandidateSearchesAndSavesPublicJobs()
    {
        var (owner, ownerId) = await fixture.User("Recruiter");
        var (otherRecruiter, otherRecruiterId) = await fixture.User("Recruiter");
        var (candidate, _) = await fixture.User("Candidate");
        var (otherCandidate, _) = await fixture.User("Candidate");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var companyResponse = await owner.PostAsJsonAsync("/api/companies", new
        {
            name = $"Example {suffix}", description = "Công ty giả dùng cho kiểm thử", websiteUrl = "",
            location = "Hà Nội", industry = "Công nghệ", sizeLabel = "11-50"
        });
        Assert.Equal(HttpStatusCode.Created, companyResponse.StatusCode);
        var company = await companyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var companyId = company.GetProperty("id").GetGuid();
        Assert.Equal(0, company.GetProperty("rowVersion").GetInt64());
        Assert.Equal(JsonValueKind.Null, company.GetProperty("websiteUrl").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.GetAsync($"/api/companies/{companyId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/companies", new
        {
            name = "Bad URL", websiteUrl = "http://example.invalid"
        })).StatusCode);

        var companyUpdate = new { rowVersion = 0L, name = $"Example Updated {suffix}", description = "Updated", websiteUrl = "https://example.invalid", location = "Đà Nẵng", industry = "Công nghệ", sizeLabel = "11-50" };
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/companies/{companyId}", companyUpdate)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/companies/{companyId}", companyUpdate)).StatusCode);

        var content = Content($"Backend {suffix}");
        var createJob = new { companyId, applicationDeadline = DateTimeOffset.UtcNow.AddDays(30), content };
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.PostAsJsonAsync("/api/jobs", createJob)).StatusCode);
        var created = await owner.PostAsJsonAsync("/api/jobs", createJob);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var draft = await created.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = draft.GetProperty("id").GetGuid();
        var draftRowVersion = draft.GetProperty("rowVersion").GetInt64();
        var firstVersionId = draft.GetProperty("version").GetProperty("id").GetGuid();
        Assert.Equal("draft", draft.GetProperty("status").GetString());
        Assert.Equal(1, draft.GetProperty("version").GetProperty("versionNo").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.App.CreateClient().GetAsync($"/api/jobs/{jobId}")).StatusCode);
        var hiddenSearch = await fixture.App.CreateClient().GetFromJsonAsync<JsonElement>($"/api/jobs?q={Uri.EscapeDataString(suffix)}");
        Assert.Equal(0, hiddenSearch.GetProperty("total").GetInt32());

        var revisedContent = Content($"Platform Engineer {suffix}");
        var editRequest = new { rowVersion = draftRowVersion, applicationDeadline = DateTimeOffset.UtcNow.AddDays(45), content = revisedContent };
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.PutAsJsonAsync($"/api/jobs/{jobId}", editRequest)).StatusCode);
        var updatedResponse = await owner.PutAsJsonAsync($"/api/jobs/{jobId}", editRequest);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updated.GetProperty("version").GetProperty("versionNo").GetInt32());
        Assert.Equal($"Platform Engineer {suffix}", updated.GetProperty("version").GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/jobs/{jobId}", editRequest)).StatusCode);

        var history = await owner.GetFromJsonAsync<JsonElement[]>($"/api/jobs/{jobId}/versions");
        Assert.Equal(2, history!.Length);
        Assert.Contains(history, x => x.GetProperty("versionNo").GetInt32() == 1 && x.GetProperty("title").GetString() == $"Backend {suffix}");
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var immutable = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE job_versions SET title = 'Tampered' WHERE id = {firstVersionId}"));
            Assert.Equal("23514", immutable.SqlState);
            var ownerGuard = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE companies SET owner_user_id = {otherRecruiterId} WHERE id = {companyId}"));
            Assert.Equal("23514", ownerGuard.SqlState);
            var companyGuard = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE jobs SET company_id = {Guid.NewGuid()} WHERE id = {jobId}"));
            Assert.Equal("23514", companyGuard.SqlState);
        }

        var updatedRowVersion = updated.GetProperty("rowVersion").GetInt64();
        var publish = await owner.PostAsJsonAsync($"/api/jobs/{jobId}/publish", new { rowVersion = updatedRowVersion });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.PostAsJsonAsync($"/api/jobs/{jobId}/publish", new { rowVersion = updatedRowVersion + 1 })).StatusCode);
        var search = await fixture.App.CreateClient().GetFromJsonAsync<JsonElement>($"/api/jobs?q={Uri.EscapeDataString(suffix)}&workMode=remote&page=1&pageSize=10");
        Assert.Equal(1, search!.GetProperty("total").GetInt32());
        Assert.Equal($"Platform Engineer {suffix}", search.GetProperty("items")[0].GetProperty("version").GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.App.CreateClient().GetAsync("/api/jobs?pageSize=51")).StatusCode);

        var savePath = $"/api/saved-jobs/{jobId}";
        Assert.Equal(HttpStatusCode.NoContent, (await candidate.PutAsync(savePath, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await candidate.PutAsync(savePath, null)).StatusCode);
        var saved = await candidate.GetFromJsonAsync<JsonElement>("/api/saved-jobs");
        Assert.Equal(1, saved!.GetProperty("total").GetInt32());
        Assert.Equal(0, (await otherCandidate.GetFromJsonAsync<JsonElement>("/api/saved-jobs")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/saved-jobs")).StatusCode);

        var expireContent = Content($"Expired after publish {suffix}");
        var expiredCreate = await owner.PostAsJsonAsync("/api/jobs", new { companyId, applicationDeadline = DateTimeOffset.UtcNow.AddDays(5), content = expireContent });
        Assert.Equal(HttpStatusCode.Created, expiredCreate.StatusCode);
        var expiringDraft = await expiredCreate.Content.ReadFromJsonAsync<JsonElement>();
        var expiringId = expiringDraft.GetProperty("id").GetGuid();
        var expiringPublish = await owner.PostAsJsonAsync($"/api/jobs/{expiringId}/publish", new { rowVersion = expiringDraft.GetProperty("rowVersion").GetInt64() });
        Assert.Equal(HttpStatusCode.OK, expiringPublish.StatusCode);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE jobs SET application_deadline = {DateTimeOffset.UtcNow.AddDays(-1)} WHERE id = {expiringId}");
        }
        var expiredPublic = await fixture.App.CreateClient().GetFromJsonAsync<JsonElement>($"/api/jobs/{expiringId}");
        Assert.False(expiredPublic!.GetProperty("acceptingApplications").GetBoolean());

        var close = await owner.PostAsJsonAsync($"/api/jobs/{jobId}/close", new { rowVersion = updatedRowVersion + 1 });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var closed = await fixture.App.CreateClient().GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}");
        Assert.Equal("closed", closed!.GetProperty("status").GetString());
        Assert.False(closed.GetProperty("acceptingApplications").GetBoolean());
        var closedUpdate = new { rowVersion = updatedRowVersion + 2, applicationDeadline = editRequest.applicationDeadline, content = revisedContent };
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/api/jobs/{jobId}", closedUpdate)).StatusCode);
        var closedSearch = await fixture.App.CreateClient().GetFromJsonAsync<JsonElement>($"/api/jobs?q={Uri.EscapeDataString(suffix)}");
        Assert.Equal(1, closedSearch!.GetProperty("total").GetInt32()); // The second, expired-but-published job remains public.
        Assert.Equal(HttpStatusCode.NoContent, (await candidate.DeleteAsync(savePath)).StatusCode);
        var emptySaved = await candidate.GetFromJsonAsync<JsonElement>("/api/saved-jobs");
        Assert.Equal(0, emptySaved!.GetProperty("total").GetInt32());
    }

    private static object Content(string title) => new
    {
        title, description = "Mô tả công việc giả bằng tiếng Việt.", requirementsText = "Yêu cầu: C#, PostgreSQL, API REST.",
        benefitsText = "Môi trường kiểm thử.", industry = "Công nghệ", location = "Hà Nội", employmentType = "full_time",
        workMode = "remote", experienceMin = 1m, experienceMax = 5m, salaryMin = (decimal?)null, salaryMax = (decimal?)null,
        salaryCurrency = (string?)null, salaryPeriod = (string?)null, language = "vi"
    };
}
