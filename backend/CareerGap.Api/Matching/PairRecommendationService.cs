using System.Security.Claims;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Matching;

public sealed record PairBatchRequest(string Direction, Guid IdempotencyKey, Guid? ResumeVersionId = null,
    Guid? JobId = null, int TopK = 3, int Page = 1);
public sealed record PairBatchItem(Guid ResumeVersionId, Guid JobVersionId, Guid? ApplicationId,
    string Label, double? RetrievalScore, Guid? PairJobId, string Status, string? ErrorCode);
public sealed record PairBatchView(string SelectionMethod, string ScoringMethod, int SearchSpaceCount,
    int SelectedCount, int Page, bool HasMore, IReadOnlyList<PairBatchItem> Items);

public sealed class PairRecommendationService(AppDb db, KeywordMatchingService baseline, PairMatchingService pairs, TimeProvider clock)
{
    public async Task<PairBatchView> Enqueue(Guid uid, PairBatchRequest request, CancellationToken ct)
    {
        if (request.IdempotencyKey == Guid.Empty || request.TopK is < 1 or > 3 || request.Page is < 1 or > 10000)
            throw new JobError("invalid_matching_batch", 400);
        var items = new List<PairBatchItem>(); int total; bool more = false;
        if (request.Direction == "jobs" && request.ResumeVersionId is { } cv)
        {
            await RequireRole(uid, "Candidate", ct);
            var run = await baseline.RecommendJobsAsync(uid, cv, request.TopK, ct);
            total = run.SearchSpaceCount;
            items.AddRange(run.Items.Select(x => new PairBatchItem(cv, x.JobVersionId, null, x.Title, x.RetrievalScore, null, "not_assessed", null)));
        }
        else if (request.Direction == "public_resumes" && request.JobId is { } job)
        {
            await RequireRole(uid, "Recruiter", ct);
            var selectedVersion = await db.Jobs.Where(x => x.Id == job).Select(x => x.CurrentVersionId).SingleOrDefaultAsync(ct);
            var run = await baseline.SearchPublicResumesAsync(uid, job, request.TopK, ct);
            var version = await db.Jobs.Where(x => x.Id == job).Select(x => x.CurrentVersionId).SingleAsync(ct);
            if (version != selectedVersion) throw new JobError("matching_selection_changed", 409);
            total = run.SearchSpaceCount;
            items.AddRange(run.Items.Select(x => new PairBatchItem(x.ResumeVersionId, version!.Value, null, x.DisplayName, x.RetrievalScore, null, "not_assessed", null)));
        }
        else if (request.Direction == "applications" && request.JobId is { } jobId)
        {
            await RequireRole(uid, "Recruiter", ct);
            if (!await (from j in db.Jobs join c in db.Companies on j.CompanyId equals c.Id
                where j.Id == jobId && c.OwnerUserId == uid && c.ArchivedAt == null select j.Id).AnyAsync(ct)) throw new JobError("not_found", 404);
            var cutoff = clock.GetUtcNow().AddDays(-90);
            var query = db.Applications.AsNoTracking().Where(x => x.JobId == jobId &&
                (x.Status != "rejected" && x.Status != "withdrawn" || x.StatusChangedAt > cutoff) &&
                db.Users.Any(u => u.Id == x.CandidateId && u.AccountStatus == "active"));
            total = await query.CountAsync(ct);
            var page = await query.OrderBy(x => x.SubmittedAt).ThenBy(x => x.Id).Skip((request.Page - 1) * 20).Take(20).ToListAsync(ct);
            more = request.Page * 20 < total;
            items.AddRange(page.Select(x => new PairBatchItem(x.SubmittedResumeVersionId, x.SubmittedJobVersionId, x.Id,
                $"Hồ sơ nộp {x.SubmittedAt:yyyy-MM-dd HH:mm} UTC", null, null, "not_assessed", null)));
        }
        else throw new JobError("invalid_matching_batch", 400);
        for (var i = 0; i < Math.Min(request.TopK, items.Count); i++)
        {
            var item = items[i];
            var context = request.Direction == "jobs" ? "self" : request.Direction == "public_resumes" ? "public_pool" : "application";
            var key = new Guid(Convert.FromHexString(PairMatchingContract.Hash($"{request.IdempotencyKey}:{context}:{item.ResumeVersionId}:{item.JobVersionId}:{item.ApplicationId}"))[..16]);
            try
            {
                var job = await pairs.Enqueue(uid, new(item.ResumeVersionId, item.JobVersionId, key, context, item.ApplicationId), ct);
                items[i] = item with { PairJobId = job.Id, Status = job.Status };
            }
            catch (JobError e)
            {
                // Admission failures roll back their short transaction; discard unsaved entities before the next pair.
                db.ChangeTracker.Clear();
                items[i] = item with { ErrorCode = e.Code };
            }
        }
        // Selection and admission are separate short operations. Recheck even unassessed
        // items before returning labels/IDs, because a public selection can be revoked meanwhile.
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            var candidate = await db.ResumeVersions.Where(x => x.Id == item.ResumeVersionId).Select(x => (Guid?)x.CandidateId).SingleOrDefaultAsync(ct);
            var context = request.Direction == "jobs" ? "self" : request.Direction == "public_resumes" ? "public_pool" : "application";
            if (candidate is null || !await PairMatchingService.CanAccess(db, uid, new MatchInput {
                CandidateId = candidate.Value, ResumeVersionId = item.ResumeVersionId, JobVersionId = item.JobVersionId,
                Context = context, ApplicationId = item.ApplicationId }, clock.GetUtcNow(), ct, checkRevisions: false))
                items.RemoveAt(i);
        }
        if (request.Direction == "public_resumes" && items.Count > 0)
        {
            // Admission and the access checks above can await database locks. Refresh all labels
            // together after them so a profile-sharing change does not leave an earlier name in the batch.
            var resumeIds = items.Select(x => x.ResumeVersionId).ToArray();
            var selectedJobVersion = items[0].JobVersionId;
            var releaseTime = clock.GetUtcNow();
            var currentLabels = await (from selection in db.CandidatePublicResumes.AsNoTracking()
                join version in db.ResumeVersions.AsNoTracking() on new { selection.ResumeVersionId, selection.CandidateId }
                    equals new { ResumeVersionId = version.Id, version.CandidateId }
                join file in db.StoredFiles.AsNoTracking() on new { version.FileId, version.CandidateId }
                    equals new { FileId = file.Id, CandidateId = file.OwnerUserId }
                join profile in db.CandidateProfiles.AsNoTracking() on selection.CandidateId equals profile.UserId
                where resumeIds.Contains(version.Id) && selection.Status == "ready" && file.Status == "ready" &&
                    file.Sha256 == selection.SourceHash && version.DocumentHash == selection.SourceHash &&
                    db.Users.Any(account => account.Id == selection.CandidateId && account.AccountStatus == "active") &&
                    db.Users.Any(account => account.Id == uid && account.AccountStatus == "active") &&
                    db.Jobs.Any(job => job.Id == request.JobId && job.CurrentVersionId == selectedJobVersion &&
                        job.Status == "published" && job.Company!.OwnerUserId == uid && job.Company.ArchivedAt == null &&
                        (job.ApplicationDeadline == null || job.ApplicationDeadline > releaseTime))
                select new
                {
                    selection.ResumeVersionId,
                    Label = db.CandidateProfileVisibility.Any(v => v.CandidateId == profile.UserId && v.FieldKey == "display_name")
                        ? profile.DisplayName : "Ứng viên"
                }).ToDictionaryAsync(x => x.ResumeVersionId, x => x.Label, ct);
            items = items.Where(x => currentLabels.ContainsKey(x.ResumeVersionId))
                .Select(x => x with { Label = currentLabels[x.ResumeVersionId] }).ToList();
        }
        return new(request.Direction == "applications" ? "application-submission-order-v1-page20" : "weighted-keyword-overlap-v1",
            PairMatchingContract.Rubric, total, items.Count(x => x.PairJobId.HasValue), request.Page, more, items);
    }

    private async Task RequireRole(Guid uid, string name, CancellationToken ct)
    {
        if (!await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == uid && r.Name == name select ur).AnyAsync(ct)) throw new JobError("forbidden", 403);
    }
}

public static class PairRecommendationEndpoints
{
    public static void MapPairRecommendations(this WebApplication app) =>
        app.MapPost("/api/matching/recommendations", async (PairBatchRequest request, ClaimsPrincipal user, PairRecommendationService service, CancellationToken ct) =>
        {
            try { return Results.Ok(await service.Enqueue(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), request, ct)); }
            catch (JobError e) { return Results.Problem(statusCode: e.Status, title: e.Code); }
            catch (KeywordMatchingError e) { return Results.Problem(statusCode: e.StatusCode, title: e.Code); }
        }).RequireAuthorization().RequireRateLimiting("matching");
}
