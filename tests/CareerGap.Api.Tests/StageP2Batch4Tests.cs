using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Learning;
using CareerGap.Api.Matching;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class StageP2Batch4Tests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handler(request, ct);
    }

    [Fact]
    public void SixCriterionGroupsAndWeeklyRoadmapCareerContextDoNotMutateRubric()
    {
        var criteria = new ScoredCriterion[]
        {
            new("r1", "Biết C# và .NET", "required", "met", 2m, 1m, [new("s1", "C# .NET", "document_span")], "ok", false, "document_text"),
            new("r2", "Có ít nhất 2 năm kinh nghiệm API", "required", "partial", 2m, 0.5m, [new("e1", "Làm API", "document_span")], "partial", true, "document_text"),
            new("r3", "Tốt nghiệp Đại học CNTT", "optional", "met", 1m, 1m, [new("ed1", "Đại học CNTT", "document_span")], "ok", false, "document_text"),
            new("r4", "Có chứng chỉ AWS", "optional", "no_evidence", 1m, 0m, [], "missing", false, "document_text")
        };

        var groups = PairCriterionGroups.Summarize(criteria);
        Assert.Equal(6, groups.Count);
        Assert.Equal(["SkillMatch", "Experience", "Achievements", "Portfolio", "Education", "AtsKeywords"], groups.Select(g => g.Key));
        Assert.Equal("not_requested", groups.Single(g => g.Key == "Portfolio").Status);
        Assert.Equal("met", groups.Single(g => g.Key == "SkillMatch").Status);
        Assert.Equal("partial", groups.Single(g => g.Key == "Experience").Status);
        Assert.Equal("met", groups.Single(g => g.Key == "Education").Status);
        Assert.Equal("no_evidence", groups.Single(g => g.Key == "Achievements").Status);
        Assert.Contains("3/4", groups.Single(g => g.Key == "AtsKeywords").Summary);

        var startPlan = WeeklyRoadmap.Build(Guid.NewGuid(), criteria, 2, 5, "career_start");
        var promoPlan = WeeklyRoadmap.Build(Guid.NewGuid(), criteria, 2, 5, "promotion");
        Assert.Equal("career_start", startPlan.CareerContext);
        Assert.NotEqual(startPlan.Schedule[0].Focus[0].Goal, promoPlan.Schedule[0].Focus[0].Goal);
    }

    [Fact]
    public async Task JdUrlImporterBlocksSsrfEnforcesSizeLimitAndExtractsCleanHtml()
    {
        var publicIp = new[] { IPAddress.Parse("93.184.216.34") };
        var privateIp = new[] { IPAddress.Parse("10.0.0.5") };
        var metadataIp = new[] { IPAddress.Parse("169.254.169.254") };

        using var okHttp = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><head><title>Ignored</title><script>alert(1)</script></head><body><h1>Senior .NET Engineer</h1><p>Required: C# and PostgreSQL</p></body></html>", Encoding.UTF8, "text/html")
        })));
        var okImporter = new JdUrlImporter(okHttp, (_, _) => Task.FromResult(publicIp));
        var preview = await okImporter.FetchAsync("https://jobs.example.com/123", CancellationToken.None);
        Assert.Equal("Senior .NET Engineer", preview.Title);
        Assert.Contains("Required: C# and PostgreSQL", preview.Text);
        Assert.DoesNotContain("alert(1)", preview.Text);

        // SSRF blocks localhost, non-standard ports, private IP, metadata IP, and redirects to internal IP.
        var blockedLocal = await Assert.ThrowsAsync<JdUrlImportException>(() => okImporter.FetchAsync("http://localhost/api/health", CancellationToken.None));
        Assert.Equal("jd_url_blocked_host", blockedLocal.Code);
        var blockedPort = await Assert.ThrowsAsync<JdUrlImportException>(() => okImporter.FetchAsync("http://localhost:5080/api/health", CancellationToken.None));
        Assert.Equal("invalid_jd_url", blockedPort.Code);

        var privateImporter = new JdUrlImporter(okHttp, (_, _) => Task.FromResult(privateIp));
        Assert.Equal("jd_url_blocked_host", (await Assert.ThrowsAsync<JdUrlImportException>(() => privateImporter.FetchAsync("https://internal.example.com/jd", CancellationToken.None))).Code);

        var metadataImporter = new JdUrlImporter(okHttp, (_, _) => Task.FromResult(metadataIp));
        Assert.Equal("jd_url_blocked_host", (await Assert.ThrowsAsync<JdUrlImportException>(() => metadataImporter.FetchAsync("http://169.254.169.254/latest/meta-data", CancellationToken.None))).Code);

        using var redirectHttp = new HttpClient(new StubHandler((_, _) =>
        {
            var msg = new HttpResponseMessage(HttpStatusCode.Found);
            msg.Headers.Location = new Uri("http://127.0.0.1:5080/secret");
            return Task.FromResult(msg);
        }));
        var redirectImporter = new JdUrlImporter(redirectHttp, (host, _) => Task.FromResult(host == "127.0.0.1" ? [IPAddress.Loopback] : publicIp));
        Assert.Equal("jd_url_blocked_host", (await Assert.ThrowsAsync<JdUrlImportException>(() => redirectImporter.FetchAsync("https://public.example.com/go", CancellationToken.None))).Code);

        using var oversizedHttp = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('A', 600 * 1024), Encoding.UTF8, "text/plain")
        })));
        var sizeImporter = new JdUrlImporter(oversizedHttp, (_, _) => Task.FromResult(publicIp));
        Assert.Equal("jd_url_too_large", (await Assert.ThrowsAsync<JdUrlImportException>(() => sizeImporter.FetchAsync("https://public.example.com/big.txt", CancellationToken.None))).Code);
    }

    [Fact]
    public async Task QuickMatchHistoryHideAndJdRewriteRunEndToEnd()
    {
        var (candidate, candidateId) = await fixture.User("Candidate");
        var (recruiter, recruiterId) = await fixture.User("Recruiter");

        // Quick match with pasted CV + pasted JD + career context (F05 + F03 + F09 + F08).
        var quickResp = await candidate.PostAsJsonAsync("/api/matching/quick-pairs", new QuickMatchRequest(
            "paste", null, "Kỹ sư phần mềm với 3 năm kinh nghiệm C#, ASP.NET Core, PostgreSQL và Docker.",
            "paste", null, "Backend .NET Developer", "Yêu cầu:\n- Thành thạo C# và ASP.NET Core\n- Có kinh nghiệm PostgreSQL\n- Biết Docker là lợi thế", null,
            Guid.NewGuid(), "career_switch"));
        Assert.Equal(HttpStatusCode.Accepted, quickResp.StatusCode);
        var quickId = (await quickResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // While queued, history hide is rejected (409 analysis_in_progress).
        Assert.Equal(HttpStatusCode.Conflict, (await candidate.DeleteAsync($"/api/matching/history/{quickId}")).StatusCode);

        // Execute worker for the queued match_pair job.
        using (var scope = fixture.App.Services.CreateScope())
        {
            var jobService = scope.ServiceProvider.GetRequiredService<JobService>();
            var claimed = await jobService.Claim("test-worker", CancellationToken.None);
            Assert.NotNull(claimed);
            Assert.Equal(quickId, claimed!.Id);
            await JobWorker.Process(scope.ServiceProvider, claimed.Id, "test-worker", claimed.LeaseGeneration, CancellationToken.None);
        }

        var pair = await candidate.GetFromJsonAsync<PairJobView>($"/api/matching/pairs/{quickId}");
        Assert.NotNull(pair);
        Assert.Equal("succeeded", pair!.Status);
        Assert.Equal("career_switch", pair.CareerContext);
        Assert.NotNull(pair.CriterionGroups);
        Assert.Equal(6, pair.CriterionGroups!.Count);

        // History lists it, then DELETE hides it without deleting AiJob or BudgetReservation.
        var historyBefore = await candidate.GetFromJsonAsync<JsonElement>("/api/matching/history");
        Assert.Contains(historyBefore.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == quickId);

        Assert.Equal(HttpStatusCode.NoContent, (await candidate.DeleteAsync($"/api/matching/history/{quickId}")).StatusCode);
        var historyAfter = await candidate.GetFromJsonAsync<JsonElement>("/api/matching/history");
        Assert.DoesNotContain(historyAfter.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == quickId);
        Assert.Equal(HttpStatusCode.NotFound, (await candidate.GetAsync($"/api/matching/pairs/{quickId}")).StatusCode);

        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.True(await db.AiJobs.AnyAsync(x => x.Id == quickId));
            Assert.True(await db.BudgetReservations.AnyAsync(x => x.AiJobId == quickId));
        }

        // Recruiter JD Rewrite (F06).
        var compResp = await recruiter.PostAsJsonAsync("/api/companies", new CompanyCreateRequest("Rewrite Co", null, "https://example.com", "Hà Nội", "CNTT", "11-50"));
        var company = (await compResp.Content.ReadFromJsonAsync<CompanyDto>())!;
        var jobContent = new JobContentRequest("Backend Engineer", "Build backend APIs", "C#\nPostgreSQL", "Bảo hiểm sức khỏe", "CNTT", "Hà Nội", "full_time", "remote", null, null, null, null, null, null, "vi");
        var jobResp = await recruiter.PostAsJsonAsync("/api/jobs", new JobCreateRequest(company.Id, null, jobContent));
        Assert.Equal(HttpStatusCode.Created, jobResp.StatusCode);
        var jobDto = (await jobResp.Content.ReadFromJsonAsync<JobDto>())!;

        var rewriteCreate = await recruiter.PostAsJsonAsync($"/api/jobs/{jobDto.Id}/rewrite-proposals", new { idempotencyKey = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Accepted, rewriteCreate.StatusCode);
        var rewriteId = (await rewriteCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using (var scope = fixture.App.Services.CreateScope())
        {
            var jobService = scope.ServiceProvider.GetRequiredService<JobService>();
            var claimed = await jobService.Claim("test-worker", CancellationToken.None);
            Assert.NotNull(claimed);
            Assert.Equal(rewriteId, claimed!.Id);
            await JobWorker.Process(scope.ServiceProvider, claimed.Id, "test-worker", claimed.LeaseGeneration, CancellationToken.None);
        }

        var proposal = await recruiter.GetFromJsonAsync<JsonElement>($"/api/writing/jd-proposals/{rewriteId}");
        Assert.Equal("succeeded", proposal.GetProperty("status").GetString());
        Assert.Contains("Backend Engineer", proposal.GetProperty("sections").GetProperty("description").GetString());
        Assert.Contains("- C#", proposal.GetProperty("sections").GetProperty("requirementsText").GetString());
    }
}
