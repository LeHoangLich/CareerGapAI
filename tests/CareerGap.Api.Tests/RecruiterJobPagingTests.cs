using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Administration;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class RecruiterJobPagingTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task OwnerCanReachEveryJobBeyondFiftyAndFilterEachStatusWithStableTies()
    {
        var (owner, _) = await fixture.User("Recruiter");
        var (other, _) = await fixture.User("Recruiter");
        var (admin, _) = await fixture.User("Admin");
        var company = await CreateCompany(owner);
        var ids = new HashSet<Guid>();
        var statusIds = new Dictionary<string, Guid>();
        for (var i = 0; i < 61; i++)
        {
            var result = await owner.PostAsJsonAsync("/api/jobs", new { companyId = company, content = Content("F17 job " + i) });
            Assert.Equal(HttpStatusCode.Created, result.StatusCode);
            var job = await result.Content.ReadFromJsonAsync<JsonElement>();
            var id = job.GetProperty("id").GetGuid(); ids.Add(id);
            if (i is >= 1 and <= 3)
            {
                var published = await fixture.PublishWithVerifiedBusinessFixture(owner, id, new { rowVersion = job.GetProperty("rowVersion").GetInt64() });
                Assert.Equal(HttpStatusCode.OK, published.StatusCode);
                job = await published.Content.ReadFromJsonAsync<JsonElement>();
                if (i == 1) statusIds["published"] = id;
                if (i == 2)
                {
                    Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync($"/api/jobs/{id}/close", new { rowVersion = job.GetProperty("rowVersion").GetInt64() })).StatusCode);
                    statusIds["closed"] = id;
                }
                if (i == 3)
                {
                    Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/jobs/{id}", new AdminAction("archive", "Synthetic F17 moderation", job.GetProperty("rowVersion").GetInt64()))).StatusCode);
                    statusIds["archived"] = id;
                }
            }
        }
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var tiedTime = DateTimeOffset.UtcNow;
            await db.Jobs.Where(x => x.CompanyId == company).ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, tiedTime));
        }
        var seen = new HashSet<Guid>();
        for (var page = 1; page <= 7; page++)
        {
            var result = await owner.GetFromJsonAsync<JsonElement>($"/api/jobs/mine?page={page}&pageSize=10");
            Assert.Equal(61, result.GetProperty("total").GetInt32()); Assert.Equal(page, result.GetProperty("page").GetInt32());
            var items = result.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(page == 7 ? 1 : 10, items.Length);
            foreach (var job in items) Assert.True(seen.Add(job.GetProperty("id").GetGuid()), "A tied timestamp must not duplicate a job across pages.");
        }
        Assert.True(ids.SetEquals(seen));
        foreach (var (status, id) in statusIds)
        {
            var filtered = await owner.GetFromJsonAsync<JsonElement>($"/api/jobs/mine?status={status}&pageSize=10");
            Assert.Equal(1, filtered.GetProperty("total").GetInt32());
            var job = Assert.Single(filtered.GetProperty("items").EnumerateArray()); Assert.Equal(id, job.GetProperty("id").GetGuid()); Assert.Equal(status, job.GetProperty("status").GetString());
        }
        var drafts = await owner.GetFromJsonAsync<JsonElement>("/api/jobs/mine?status=draft&page=6&pageSize=10");
        Assert.Equal(58, drafts.GetProperty("total").GetInt32()); Assert.Equal(8, drafts.GetProperty("items").GetArrayLength());
        var stranger = await other.GetFromJsonAsync<JsonElement>("/api/jobs/mine?status=draft"); Assert.Equal(0, stranger.GetProperty("total").GetInt32());
        var outside = await owner.GetFromJsonAsync<JsonElement>("/api/jobs/mine?page=999&pageSize=10"); Assert.Empty(outside.GetProperty("items").EnumerateArray());
        using var verifyScope = fixture.App.Services.CreateScope();
        Assert.Equal(61, await verifyScope.ServiceProvider.GetRequiredService<AppDb>().Jobs.CountAsync(x => x.CompanyId == company));
    }

    [Fact]
    public async Task FiltersRejectUnknownStatesAndKeepRecruiterAuthorization()
    {
        var (owner, _) = await fixture.User("Recruiter"); var (candidate, _) = await fixture.User();
        foreach (var query in new[] { "status=pending", "status=accepted", "status=Published", "page=0", "pageSize=51" })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync("/api/jobs/mine?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.GetAsync("/api/jobs/mine?status=archived")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.App.CreateClient().GetAsync("/api/jobs/mine?status=draft")).StatusCode);
    }

    private async Task<Guid> CreateCompany(HttpClient client)
    {
        var result = await client.PostAsJsonAsync("/api/companies", new { name = "F17 synthetic " + Guid.NewGuid(), description = "Test only", websiteUrl = "", location = "Hà Nội", industry = "CNTT", sizeLabel = "11-50" });
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static object Content(string title) => new
    {
        title, description = "Synthetic F17 job for pagination and ownership verification.", requirementsText = "React, PostgreSQL and automated testing.",
        benefitsText = "Test data only", industry = "CNTT", location = "Hà Nội", employmentType = "full_time", workMode = "remote",
        experienceMin = 1m, experienceMax = 3m, salaryMin = 1200.25m, salaryMax = 2400.5m, salaryCurrency = "USD", salaryPeriod = "month", language = "vi"
    };
}
