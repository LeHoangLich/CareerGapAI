using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Community;
using CareerGap.Api.Data;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class StageP3DiscoveryTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task JobDetailTracksDeduplicatedViewsPerViewerPerDayAndExposesCount()
    {
        var (recruiter, recruiterId) = await fixture.User("Recruiter");
        var compResp = await recruiter.PostAsJsonAsync("/api/companies", new CompanyCreateRequest("View Co", null, "https://example.com", "Hà Nội", "CNTT", "11-50"));
        var company = (await compResp.Content.ReadFromJsonAsync<CompanyDto>())!;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var identity = await db.BusinessIdentities.SingleAsync(x => x.UserId == recruiterId);
            identity.Status = "verified";
            await db.SaveChangesAsync();
        }
        var content = new JobContentRequest("F14 View Job", "API", "C#", null, "CNTT", "Hà Nội", "full_time", "remote", null, null, null, null, null, null, "vi");
        var create = await recruiter.PostAsJsonAsync("/api/jobs", new JobCreateRequest(company.Id, null, content));
        var job = (await create.Content.ReadFromJsonAsync<JobDto>())!;
        await recruiter.PostAsJsonAsync($"/api/jobs/{job.Id}/publish", new JobStateRequest(job.RowVersion));

        var guest1 = fixture.App.CreateClient();
        var view1 = await guest1.GetFromJsonAsync<JobDto>($"/api/jobs/{job.Id}");
        Assert.Equal(1, view1!.ViewCount);

        // Reload with same cookie in the same day does not inflate viewCount.
        var view1Again = await guest1.GetFromJsonAsync<JobDto>($"/api/jobs/{job.Id}");
        Assert.Equal(1, view1Again!.ViewCount);

        // Different authenticated viewer increments viewCount to 2.
        var (candidate, _) = await fixture.User("Candidate");
        var view2 = await candidate.GetFromJsonAsync<JobDto>($"/api/jobs/{job.Id}");
        Assert.Equal(2, view2!.ViewCount);

        var mine = await recruiter.GetFromJsonAsync<JobPage<JobDto>>("/api/jobs/mine");
        Assert.Equal(2, Assert.Single(mine!.Items, x => x.Id == job.Id).ViewCount);
    }

    [Fact]
    public async Task CommunityTrendingSearchesDeduplicateAndBlockPiiOrUnmatchedQueries()
    {
        var (author, _) = await fixture.User("Candidate");
        var (admin, _) = await fixture.User("Admin");
        var profileResp = await author.PutAsJsonAsync("/api/community/me", new ProfileEdit("trend_author", "Trend Author", "Bio", null, true, 0));
        Assert.Equal(HttpStatusCode.OK, profileResp.StatusCode);

        var create = await author.PostAsJsonAsync("/api/community/posts", new PostEdit(
            "Kinh nghiệm phỏng vấn React và Docker", "Tóm tắt React",
            "Nội dung chi tiết về React và Docker trong thực tế dự án.",
            "technical", ["react", "docker"], null, true, 0));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var post = (await create.Content.ReadFromJsonAsync<SocialPost>())!;
        var approve = await admin.PostAsJsonAsync($"/api/community/posts/{post.Id}/state", new ModerationAction("approve", "Approved", post.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var viewer1 = fixture.App.CreateClient();
        // Valid matching query counted once per viewer/day.
        Assert.Equal(HttpStatusCode.OK, (await viewer1.GetAsync("/api/community/posts?mode=discover&q=React")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer1.GetAsync("/api/community/posts?mode=discover&q=react")).StatusCode);

        // Second viewer increments count to 2.
        var viewer2 = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await viewer2.GetAsync("/api/community/posts?mode=discover&q=React")).StatusCode);

        // PII / unmatched private strings must never be recorded.
        await viewer1.GetAsync("/api/community/posts?mode=discover&q=user@example.com");
        await viewer1.GetAsync("/api/community/posts?mode=discover&q=0901234567");
        await viewer1.GetAsync("/api/community/posts?mode=discover&q=SecretPrivateCvText");

        var trending = await viewer1.GetFromJsonAsync<JsonElement[]>("/api/community/trending-searches");
        var reactItem = Assert.Single(trending!, x => x.GetProperty("term").GetString() == "react");
        Assert.Equal(2, reactItem.GetProperty("count").GetInt32());
        Assert.DoesNotContain(trending!, x => x.GetProperty("term").GetString()!.Contains("example.com"));
        Assert.DoesNotContain(trending!, x => x.GetProperty("term").GetString()!.Contains("0901234567"));
        Assert.DoesNotContain(trending!, x => x.GetProperty("term").GetString()!.Contains("secretprivatecvtext"));
    }
}
