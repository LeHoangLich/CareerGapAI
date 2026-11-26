using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class RecruiterApplicationPagingTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task PagingAndStatusFiltersKeepEveryVisibleApplicationAndRespectRetentionAndOwnership()
    {
        var (owner, _) = await fixture.User("Recruiter");
        var (other, _) = await fixture.User("Recruiter");
        var company = await owner.PostAsJsonAsync("/api/companies", new { name = "F18 synthetic", location = "Hà Nội" });
        Assert.Equal(HttpStatusCode.Created, company.StatusCode);
        var companyId = (await company.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var created = await owner.PostAsJsonAsync("/api/jobs", new { companyId, content = new {
            title = "F18 synthetic paging", description = "Test only", requirementsText = "React", employmentType = "full_time", workMode = "remote", language = "vi"
        }});
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var job = await created.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await fixture.PublishWithVerifiedBusinessFixture(owner, jobId, new { rowVersion = job.GetProperty("rowVersion").GetInt64() })).StatusCode);
        var expected = new Dictionary<Guid,string>();
        Guid inactiveCandidate = default, expiredRejected = default, expiredWithdrawn = default;
        HttpClient? firstCandidate = null;
        var tied = DateTimeOffset.UtcNow;
        using var seedScope = fixture.App.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDb>();
        for (var i = 0; i < 64; i++)
        {
            var (candidate, candidateId) = await fixture.User(); firstCandidate ??= candidate;
            var resume = await FoundationFixture.Upload(candidate);
            var id = Guid.NewGuid();
            var status = i switch { 57 => "reviewing", 58 => "shortlisted", 59 or 62 => "rejected", 60 or 63 => "withdrawn", _ => "submitted" };
            // Insert tied timestamps in the fixture; never update the immutable submission snapshot.
            seedDb.Applications.Add(new ApplicationRecord {
                Id = id, CandidateId = candidateId, JobId = jobId,
                SubmittedResumeVersionId = resume.GetProperty("versionId").GetGuid(),
                SubmittedJobVersionId = job.GetProperty("version").GetProperty("id").GetGuid(),
                Status = status, SubmittedAt = tied, StatusChangedAt = tied,
                WithdrawnAt = status == "withdrawn" ? tied : null,
                SubmissionKey = Guid.NewGuid(), SubmissionHash = new string('a', 64)
            });
            if (i < 61) expected[id] = status;
            else if (i == 61) inactiveCandidate = candidateId;
            else if (i == 62) expiredRejected = id;
            else expiredWithdrawn = id;
        }
        await seedDb.SaveChangesAsync();
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.Users.Where(x => x.Id == inactiveCandidate).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "suspended"));
            await db.Applications.Where(x => x.Id == expiredRejected || x.Id == expiredWithdrawn)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StatusChangedAt, tied.AddDays(-91)));
        }
        var seen = new HashSet<Guid>();
        var ordered = new List<Guid>();
        for (var page = 1; page <= 7; page++)
        {
            var result = await owner.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}/applications?page={page}&pageSize=10");
            Assert.Equal(61, result.GetProperty("total").GetInt32());
            var items = result.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(page == 7 ? 1 : 10, items.Length);
            foreach (var item in items)
            {
                var id = item.GetProperty("id").GetGuid();
                Assert.True(seen.Add(id)); ordered.Add(id);
            }
        }
        Assert.True(expected.Keys.ToHashSet().SetEquals(seen));
        Assert.Equal(expected.Keys.OrderByDescending(id => id.ToString("N"), StringComparer.Ordinal), ordered);
        foreach (var status in new[] { "submitted", "reviewing", "shortlisted", "rejected", "withdrawn" })
        {
            var filteredIds = new HashSet<Guid>();
            var count = expected.Count(x => x.Value == status);
            for (var page = 1; page <= (count + 9) / 10; page++)
            {
                var result = await owner.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}/applications?status={status}&page={page}&pageSize=10");
                Assert.Equal(count, result.GetProperty("total").GetInt32());
                foreach (var item in result.GetProperty("items").EnumerateArray())
                {
                    Assert.Equal(status, item.GetProperty("status").GetString());
                    Assert.True(filteredIds.Add(item.GetProperty("id").GetGuid()));
                }
            }
            Assert.True(expected.Where(x => x.Value == status).Select(x => x.Key).ToHashSet().SetEquals(filteredIds));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/jobs/{jobId}/applications?status=submitted")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await firstCandidate!.GetAsync($"/api/jobs/{jobId}/applications?status=submitted")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.App.CreateClient().GetAsync($"/api/jobs/{jobId}/applications")).StatusCode);
        foreach (var query in new[] { "status=accepted", "status=Submitted", "page=0", "pageSize=51" })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync($"/api/jobs/{jobId}/applications?{query}")).StatusCode);
        var outside = await owner.GetFromJsonAsync<JsonElement>($"/api/jobs/{jobId}/applications?status=reviewing&page=999&pageSize=10");
        Assert.Equal(1, outside.GetProperty("total").GetInt32()); Assert.Empty(outside.GetProperty("items").EnumerateArray());
    }
}
