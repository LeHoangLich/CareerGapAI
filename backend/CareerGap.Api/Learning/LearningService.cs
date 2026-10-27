using System.Security.Claims;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Integrations;
using CareerGap.Api.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareerGap.Api.Learning;

public sealed record LearningTopic(string Key, string Name, string Aliases);
public sealed record LearningQuery(Guid PairJobId, string RequirementKey, string TopicKey, Guid IdempotencyKey, string Language = "vi", string Level = "beginner");
public sealed class LearningService(AppDb db, PairMatchingService pairs, IWebSearchClient search, IOptions<SearchOptions> options, IConfiguration configuration, TimeProvider clock)
{
    // A query vocabulary, not a CV taxonomy. Only approved topic names leave the server.
    public static readonly LearningTopic[] Topics = [
        new("docker", "Docker", "Docker containers"), new("react", "React", "React React.js ReactJS"), new("typescript", "TypeScript", "TypeScript"),
        new("python", "Python", "Python"), new("sql", "SQL", "SQL"), new("postgresql", "PostgreSQL", "PostgreSQL"), new("java", "Java", "Java"),
        new("javascript", "JavaScript", "JavaScript"), new("dotnet", ".NET", ".NET ASP.NET C#"), new("git", "Git", "Git"), new("testing", "Software testing", "testing kiểm thử"),
        new("excel", "Microsoft Excel", "Excel"), new("accounting", "Accounting", "accounting kế toán"), new("marketing", "Digital marketing", "marketing tiếp thị"),
        new("communication", "Communication skills", "communication giao tiếp"), new("management", "Project management", "project management quản lý dự án"),
        new("english", "Business English", "English tiếng Anh"), new("analytics", "Data analysis", "analytics analysis phân tích dữ liệu"), new("design", "Graphic design", "design thiết kế đồ họa"),
        new("sales", "Sales skills", "sales bán hàng"), new("customer-service", "Customer service", "customer service chăm sóc khách hàng")];
    private async Task<ScoredCriterion> Gap(Guid uid, Guid pairId, string key, CancellationToken ct)
    {
        var pair = await pairs.Read(uid, pairId, ct);
        if (pair?.Context != "self" || pair.Status != "succeeded") throw new JobError("not_found", 404);
        return pair.Criteria?.SingleOrDefault(x => x.RequirementKey == key && x.Status is "partial" or "no_evidence") ?? throw new JobError("learning_gap_required", 409);
    }
    public async Task<IReadOnlyList<LearningTopic>> Choices(Guid uid, Guid pair, string key, CancellationToken ct)
    { var gap = await Gap(uid, pair, key, ct); return Topics.Where(t => PairMatchingContract.HasOverlap(gap.Requirement, [t.Aliases])).Take(6).ToArray(); }
    public async Task<object> Find(Guid uid, LearningQuery request, CancellationToken ct)
    {
        if (request.IdempotencyKey == Guid.Empty || request.Language is not ("vi" or "en") || request.Level is not ("beginner" or "intermediate")) throw new JobError("invalid_learning_query", 400);
        var choices = await Choices(uid, request.PairJobId, request.RequirementKey, ct);
        var topic = choices.SingleOrDefault(t => t.Key == request.TopicKey) ?? throw new JobError("learning_topic_unavailable", 422);
        var opt = options.Value; var now = clock.GetUtcNow(); var isFake = opt.Provider == "Fake";
        if (!isFake && (!opt.Ready(now) || string.IsNullOrWhiteSpace(configuration["Search:ApiKey"]))) throw new JobError("search_not_configured", 503);
        var start = isFake ? new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero) : DateTimeOffset.Parse(opt.PeriodStart).ToUniversalTime();
        var end = isFake ? start.AddMonths(1) : DateTimeOffset.Parse(opt.PeriodEnd).ToUniversalTime();
        var hash = PairMatchingContract.Hash($"learning-topics-v1:{request.PairJobId}:{request.RequirementKey}:{topic.Key}:{request.Language}:{request.Level}:{opt.Provider}");
        LearningRequest row;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(341018)", ct);
            var previous = await db.LearningRequests.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == uid && x.IdempotencyKey == request.IdempotencyKey, ct);
            if (previous is not null)
            {
                if (previous.InputHash != hash) throw new JobError("idempotency_conflict", 409);
                await tx.CommitAsync(ct); return View(previous, now, opt.CacheTermsVerified && opt.MetadataCacheMinutes > 0);
            }
            var cached = await db.LearningRequests.AsNoTracking().Where(x => x.OwnerId == uid && x.InputHash == hash && x.Status == "succeeded" && x.ExpiresAt > now && x.LinksJson != null).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
            if (cached is not null && opt.CacheTermsVerified && opt.MetadataCacheMinutes > 0)
            {
                row = new LearningRequest { OwnerId = uid, PairJobId = request.PairJobId, RequirementKey = request.RequirementKey, Topic = topic.Key,
                    Language = request.Language, Level = request.Level, IdempotencyKey = request.IdempotencyKey, InputHash = hash, Provider = opt.Provider,
                    PeriodStart = start, PeriodEnd = end, ReservedCredits = 0, ActualCredits = 0, Status = "cached", LinksJson = cached.LinksJson, ExpiresAt = cached.ExpiresAt, CreatedAt = now };
                db.LearningRequests.Add(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return View(row, now, true);
            }
            // A changed period boundary must not silently replenish an overlapping credit window.
            if (!isFake && await db.LearningRequests.AnyAsync(x => x.Provider == opt.Provider && x.PeriodStart < end && x.PeriodEnd > start && (x.PeriodStart != start || x.PeriodEnd != end), ct))
                throw new JobError("search_period_conflict", 409);
            var period = db.LearningRequests.Where(x => x.Provider == opt.Provider && x.PeriodStart == start);
            if (!isFake && (await period.SumAsync(x => x.ActualCredits ?? x.ReservedCredits, ct) + 1 > opt.CreditLimit ||
                await period.Where(x => x.OwnerId == uid).SumAsync(x => x.ActualCredits ?? x.ReservedCredits, ct) + 1 > opt.UserCreditLimit)) throw new JobError("search_quota_exhausted", 429);
            row = new LearningRequest { OwnerId = uid, PairJobId = request.PairJobId, RequirementKey = request.RequirementKey, Topic = topic.Key,
                Language = request.Language, Level = request.Level, IdempotencyKey = request.IdempotencyKey, InputHash = hash, Provider = opt.Provider,
                PeriodStart = start, PeriodEnd = end, ReservedCredits = isFake ? 0 : 1, CreatedAt = now };
            db.LearningRequests.Add(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        SearchReply? reply = null;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
            reply = await search.SearchLearningAsync(topic.Name, request.Level, request.Language, timeout.Token);
            if (reply.CreditsUsed is < 0 or > 100) throw new IntegrationError("search_usage_unknown");
            reply = reply with { Links = reply.Links.Where(x => !string.IsNullOrWhiteSpace(x.Title) && IntegrationHttp.SafePublicLink(x.Url, out _)).Take(3)
                .Select(x => x with { Title = x.Title[..Math.Min(x.Title.Length, 250)], Source = new Uri(x.Url).IdnHost, Language = request.Language }).DistinctBy(x => x.Url).ToArray() };
            row.Status = reply.Links.Count == 0 ? "no_results" : "succeeded"; row.ActualCredits = reply.CreditsUsed;
            if (reply.CreditsUsed is null) row.Status = "outcome_unknown";
            if (opt.CacheTermsVerified && opt.MetadataCacheMinutes > 0 && reply.CreditsUsed is not null)
            { row.LinksJson = PairMatchingContract.Serialize(reply.Links); row.ExpiresAt = now.AddMinutes(Math.Clamp(opt.MetadataCacheMinutes, 1, 60)); }
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or IntegrationError or JsonException)
        { row.Status = "outcome_unknown"; await db.SaveChangesAsync(CancellationToken.None); throw new JobError(error is IntegrationError e ? e.Code : "search_unavailable", 503); }
        await Gap(uid, request.PairJobId, request.RequirementKey, ct);
        return new { row.Id, row.Status, provider = row.Provider, links = reply!.Links, languageBasis = "query", reason = $"Nguồn tìm cho {topic.Name}; tiêu chí này chưa có bằng chứng đầy đủ trong CV. Chưa đọc toàn bộ trang.", cached = false };
    }
    private static object View(LearningRequest row, DateTimeOffset now, bool cacheAllowed) => new { row.Id, status = row.Status == "sent" ? "outcome_unknown" : row.Status is "succeeded" or "cached" && (!cacheAllowed || row.LinksJson == null || row.ExpiresAt <= now) ? "results_not_retained" : row.Status,
        provider = row.Provider, links = cacheAllowed && row.ExpiresAt > now && row.LinksJson != null ? JsonSerializer.Deserialize<LearningLink[]>(row.LinksJson, OutputValidator.Json) : [], languageBasis = "query", cached = true };
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/learning/topics", async (Guid pairJobId, string requirementKey, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
        { try { return Results.Ok(await service.Choices(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), pairJobId, requirementKey, ct)); } catch (JobError e) { return Results.Problem(statusCode: e.Status, title: e.Code); } }).RequireAuthorization(p => p.RequireRole("Candidate"));
        app.MapPost("/api/learning/resources", async (LearningQuery request, ClaimsPrincipal user, LearningService service, CancellationToken ct) =>
        { try { return Results.Ok(await service.Find(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), request, ct)); } catch (JobError e) { return Results.Problem(statusCode: e.Status, title: e.Code); } }).RequireAuthorization(p => p.RequireRole("Candidate")).RequireRateLimiting("matching");
    }
}
