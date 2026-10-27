using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Integrations;
using CareerGap.Api.Learning;
using CareerGap.Api.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
namespace CareerGap.Api.Tests;

public sealed partial class PairMatchingTests
{
    private sealed class SearchSpy(bool unknown = false) : IWebSearchClient
    {
        public int Calls; public string Query = "";
        public Task<SearchReply> SearchLearningAsync(string skill, string level, string language, CancellationToken ct)
        { Calls++; Query = skill; if (unknown) throw new HttpRequestException("offline"); return Task.FromResult(new SearchReply([new("Synthetic search result", "https://example.org/course", "ignored", language), new("Unsafe", "http://localhost/private", "ignored", language)], 1)); }
    }
    [Fact] public async Task LearningReservesCreditsBindsCacheKeyAndRechecksPairRights()
    {
        var s = await Prepare(); var id = await Enqueue(s.Candidate, s); await Drain(id);
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.LearningRequests.ExecuteDeleteAsync();
        var pairs = scope.ServiceProvider.GetRequiredService<PairMatchingService>(); var pair = await pairs.Read(s.CandidateId, id, default);
        var gap = pair!.Criteria!.First(x => x.Requirement.Contains("Docker")).RequirementKey;
        var opt = new SearchOptions { Provider = "Tavily", CreditLimit = 1, UserCreditLimit = 1, PeriodStart = DateTimeOffset.UtcNow.AddHours(-1).ToString("O"), PeriodEnd = DateTimeOffset.UtcNow.AddDays(1).ToString("O"), CreditsVerifiedAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"), CacheTermsVerified = true, MetadataCacheMinutes = 1 };
        var spy = new SearchSpy(); var service = new LearningService(db, pairs, spy, Options.Create(opt), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Search:ApiKey"] = "offline-test-only" }).Build(), TimeProvider.System);
        var request = new LearningQuery(id, gap, "docker", Guid.NewGuid());
        var first = JsonSerializer.SerializeToElement(await service.Find(s.CandidateId, request, default), OutputValidator.Json);
        Assert.Equal(1, spy.Calls); Assert.Equal("Docker", spy.Query); Assert.Single(first.GetProperty("links").EnumerateArray());
        var second = request with { IdempotencyKey = Guid.NewGuid() }; await service.Find(s.CandidateId, second, default); Assert.Equal(1, spy.Calls);
        opt.CacheTermsVerified = false;
        var disabledCache = JsonSerializer.SerializeToElement(await service.Find(s.CandidateId, request, default), OutputValidator.Json);
        Assert.Empty(disabledCache.GetProperty("links").EnumerateArray()); Assert.Equal(1, spy.Calls);
        opt.CacheTermsVerified = true;
        Assert.Equal("idempotency_conflict", (await Assert.ThrowsAsync<JobError>(() => service.Find(s.CandidateId, second with { Language = "en" }, default))).Code);
        Assert.Equal(1, await db.LearningRequests.SumAsync(x => x.ActualCredits ?? x.ReservedCredits));
        Assert.Equal("search_quota_exhausted", (await Assert.ThrowsAsync<JobError>(() => service.Find(s.CandidateId, request with { IdempotencyKey = Guid.NewGuid(), Language = "en" }, default))).Code);
        Assert.Equal("not_found", (await Assert.ThrowsAsync<JobError>(() => service.Find(Guid.NewGuid(), request, default))).Code);
        await db.LearningRequests.ExecuteUpdateAsync(set => set.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        Assert.Equal(2, await LearningCacheWorker.Expire(db, DateTimeOffset.UtcNow, default));
        Assert.False(await db.LearningRequests.AnyAsync(x => x.LinksJson != null));
    }
    [Fact] public async Task LearningUnknownKeepsReservationAndNeverAutomaticallyResends()
    {
        var s = await Prepare(); var id = await Enqueue(s.Candidate, s); await Drain(id);
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>(); var pairs = scope.ServiceProvider.GetRequiredService<PairMatchingService>();
        var gap = (await pairs.Read(s.CandidateId, id, default))!.Criteria!.First(x => x.Requirement.Contains("Docker")).RequirementKey;
        var opt = new SearchOptions { Provider = "Tavily", CreditLimit = 1, UserCreditLimit = 1, PeriodStart = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O"), PeriodEnd = DateTimeOffset.UtcNow.AddHours(3).ToString("O"), CreditsVerifiedAt = DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O") };
        // This fixture may contain a previous period: isolate this test's synthetic provider ledger.
        await db.LearningRequests.ExecuteDeleteAsync();
        var spy = new SearchSpy(true); var service = new LearningService(db, pairs, spy, Options.Create(opt), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Search:ApiKey"] = "offline-test-only" }).Build(), TimeProvider.System);
        var request = new LearningQuery(id, gap, "docker", Guid.NewGuid());
        await Assert.ThrowsAsync<JobError>(() => service.Find(s.CandidateId, request, default));
        var retry = JsonSerializer.SerializeToElement(await service.Find(s.CandidateId, request, default), OutputValidator.Json);
        Assert.Equal("outcome_unknown", retry.GetProperty("status").GetString()); Assert.Equal(1, spy.Calls);
        Assert.Equal(1, await db.LearningRequests.SumAsync(x => x.ActualCredits ?? x.ReservedCredits));
        Assert.Equal(HttpStatusCode.Accepted, (await s.Candidate.PostAsJsonAsync("/api/account/deletion-request", new { password = CandidatePassword })).StatusCode);
        fixture.Clock.Advance(TimeSpan.FromDays(8));
        try
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<CareerGap.Api.Accounts.AccountDeletionService>().ProcessOneDueAsync("learning-purge", default));
            var retained = await db.LearningRequests.AsNoTracking().SingleAsync();
            Assert.Null(retained.PairJobId); Assert.Null(retained.LinksJson); Assert.Equal("", retained.RequirementKey);
            Assert.Null(retained.ActualCredits); Assert.Equal(1, retained.ReservedCredits);
        }
        finally { fixture.Clock.Advance(TimeSpan.FromDays(-8)); }
    }
}

public sealed class SearchAdapterTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public string Body = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("https://api.tavily.com/search", request.RequestUri!.AbsoluteUri); Body = await request.Content!.ReadAsStringAsync(ct);
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"usage\":{\"credits\":1},\"results\":[{\"title\":\"Course\",\"url\":\"https://example.org/course\"},{\"title\":\"Private\",\"url\":\"https://127.0.0.1/private\"}]}") };
        }
    }
    [Fact] public async Task AdapterUsesFixedBasicQueryAndToolUrlsOnly()
    {
        var handler = new Handler(); var client = new TavilySearchClient(new HttpClient(handler), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Search:ApiKey"] = "offline" }).Build());
        var reply = await client.SearchLearningAsync("Docker", "beginner", "vi", default);
        Assert.Single(reply.Links); Assert.Equal(1, reply.CreditsUsed);
        using var body = JsonDocument.Parse(handler.Body); Assert.Equal("basic", body.RootElement.GetProperty("search_depth").GetString()); Assert.False(body.RootElement.GetProperty("auto_parameters").GetBoolean()); Assert.False(body.RootElement.GetProperty("include_raw_content").GetBoolean());
    }
    [Theory][InlineData("http://example.org")][InlineData("https://localhost/x")][InlineData("https://10.0.0.1/x")][InlineData("https://user:pass@example.org/x")][InlineData("https://example.org:444/x")]
    public void RejectsUnsafeLinks(string url) => Assert.False(IntegrationHttp.SafePublicLink(url, out _));
}
