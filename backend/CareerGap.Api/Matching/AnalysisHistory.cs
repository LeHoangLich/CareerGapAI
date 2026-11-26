using System.Security.Claims;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Matching;

public sealed record AnalysisHistoryItem(Guid Id, DateTimeOffset CreatedAt, string Status, string Context,
    bool Available, string? JobTitle, int? ResumeVersionNo, decimal? Score);

public static class AnalysisHistory
{
    public static void MapAnalysisHistory(this WebApplication app)
    {
        app.MapGet("/api/matching/history", async (string? status, string? context, int? page, int? pageSize,
            ClaimsPrincipal user, AppDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (page is < 1 or > 10000 || pageSize is < 1 or > 50 ||
                status is not (null or "all" or "queued" or "running" or "succeeded" or "failed" or "cancelled") ||
                context is not (null or "all" or "self" or "public_pool" or "application"))
                return Results.Problem(statusCode:400, title:"invalid_history_filter");
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var query = from job in db.AiJobs.AsNoTracking()
                join input in db.MatchInputs.AsNoTracking() on job.Id equals input.AiJobId
                where job.RequestedBy == uid && job.JobType == "match_pair" && !db.AnalysisHistoryHides.Any(h => h.AiJobId == job.Id)
                select new { job.Id, job.CreatedAt, job.Status, Input = input };
            if (status is not (null or "all")) query = query.Where(x => x.Status == status);
            if (context is not (null or "all")) query = query.Where(x => x.Input.Context == context);
            var total = await query.CountAsync(ct);
            var current = page ?? 1; var size = pageSize ?? 10;
            var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                .Skip((current-1)*size).Take(size).ToArrayAsync(ct);
            var items = new List<AnalysisHistoryItem>();
            foreach (var row in rows)
            {
                // A requester can see that their run existed, never revoked source metadata or score.
                var available = await PairMatchingService.CanAccess(db, uid, row.Input, clock.GetUtcNow(), ct);
                string? title = null; int? version = null; decimal? score = null;
                if (available)
                {
                    title = row.Input.JobVersionId is { } jvid
                        ? await db.JobVersions.Where(x => x.Id == jvid).Select(x => x.Title).SingleOrDefaultAsync(ct)
                        : await db.QuickMatchJds.Where(x => x.Id == row.Input.QuickMatchJdId).Select(x => x.Title).SingleOrDefaultAsync(ct);
                    version = await db.ResumeVersions.Where(x => x.Id == row.Input.ResumeVersionId).Select(x => (int?)x.VersionNo).SingleOrDefaultAsync(ct);
                    score = await db.MatchResults.Where(x => x.AiJobId == row.Id).Select(x => (decimal?)x.Score).SingleOrDefaultAsync(ct);
                    available = await PairMatchingService.CanAccess(db, uid, row.Input, clock.GetUtcNow(), ct);
                }
                items.Add(new(row.Id, row.CreatedAt, row.Status, row.Input.Context, available,
                    available ? title : null, available ? version : null, available && row.Status == "succeeded" ? score : null));
            }
            return Results.Ok(new { items, total, page = current, pageSize = size });
        }).RequireAuthorization(p => p.RequireRole("Candidate", "Recruiter"));

        app.MapDelete("/api/matching/history/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var row = await (from job in db.AiJobs.AsNoTracking()
                join input in db.MatchInputs.AsNoTracking() on job.Id equals input.AiJobId
                where job.Id == id && job.RequestedBy == uid && job.JobType == "match_pair" && !db.AnalysisHistoryHides.Any(h => h.AiJobId == job.Id)
                select new { job.Status }).SingleOrDefaultAsync(ct);
            if (row is null) return Results.NotFound();
            if (row.Status is "queued" or "running")
                return Results.Problem(statusCode: 409, title: "analysis_in_progress");
            var hiddenAt = clock.GetUtcNow().ToUniversalTime();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO analysis_history_hides (ai_job_id, hidden_at) VALUES ({id}, {hiddenAt}) ON CONFLICT (ai_job_id) DO NOTHING", ct);
            return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole("Candidate", "Recruiter"));
    }
}
