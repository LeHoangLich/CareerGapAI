using System.Security.Claims;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Matching;

public sealed record CreatePairRequest(Guid ResumeVersionId, Guid JobVersionId, Guid IdempotencyKey,
    string Context = "self", Guid? ApplicationId = null);
public sealed record PairJobView(Guid Id, string Status, string? ErrorCode, string Provider, string Method,
    string Rubric, string Context, Guid ResumeVersionId, Guid ResumeRevisionId, Guid JobVersionId,
    Guid JobRevisionId, Guid? ApplicationId, decimal? Score, IReadOnlyList<ScoredCriterion>? Criteria,
    string? Location, string WorkMode, decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency,
    string? SalaryPeriod, string ConditionsStatus = "not_assessed_no_confirmed_preferences");

public sealed class PairMatchingService(AppDb db, JobService jobs, TimeProvider clock)
{
    public async Task<AiJob> Enqueue(Guid uid, CreatePairRequest request, CancellationToken ct)
    {
        if (request.Context is not ("self" or "public_pool" or "application") ||
            (request.Context == "application") != request.ApplicationId.HasValue) throw new JobError("invalid_match_context", 400);
        var cv = await db.ResumeVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ResumeVersionId, ct) ?? throw new JobError("not_found", 404);
        var version = await db.JobVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.JobVersionId, ct) ?? throw new JobError("not_found", 404);
        var input = new MatchInput { CandidateId = cv.CandidateId, ResumeVersionId = cv.Id, JobVersionId = version.Id,
            Context = request.Context, ApplicationId = request.ApplicationId };
        // Check authorization before reading revisions or reporting their state.
        if (!await CanAccess(db, uid, input, clock.GetUtcNow(), ct, checkRevisions: false)) throw new JobError("not_found", 404);
        var previous = await db.AiJobs.AsNoTracking().SingleOrDefaultAsync(x => x.RequestedBy == uid && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (previous is not null)
        {
            var pinned = await db.MatchInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == previous.Id, ct);
            if (previous.JobType != "match_pair" || pinned is null || pinned.ResumeVersionId != request.ResumeVersionId ||
                pinned.JobVersionId != request.JobVersionId || pinned.Context != request.Context || pinned.ApplicationId != request.ApplicationId)
                throw new JobError("idempotency_conflict", 409);
            if (!await CanAccess(db, uid, pinned, clock.GetUtcNow(), ct)) throw new JobError("not_found", 404);
            return previous;
        }
        DateTimeOffset? asOf = request.ApplicationId is { } appId
            ? (await db.Applications.AsNoTracking().SingleAsync(x => x.Id == appId, ct)).SubmittedAt : null;
        var owner = await (from j in db.Jobs join c in db.Companies on j.CompanyId equals c.Id
            where j.Id == version.JobId select c.OwnerUserId).SingleAsync(ct);
        var cvRevision = await db.ResumeDataRevisions.AsNoTracking().Where(x => x.ResumeVersionId == cv.Id && x.CandidateId == cv.CandidateId &&
            (x.Origin == "user_confirmed" || x.Origin == "user_entered" && x.SchemaVersion == 2) && x.ConfirmedBy == cv.CandidateId && x.ConfirmedAt != null &&
            (asOf == null || x.CreatedAt <= asOf && x.ConfirmedAt <= asOf)).OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct)
            ?? throw new JobError("confirmed_resume_required", 409);
        var jdRevision = await db.JobDataRevisions.AsNoTracking().Where(x => x.JobVersionId == version.Id &&
            x.Origin == "user_confirmed" && x.ConfirmedBy == owner && x.ConfirmedAt != null &&
            (asOf == null || x.CreatedAt <= asOf && x.ConfirmedAt <= asOf)).OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct)
            ?? throw new JobError("confirmed_job_required", 409);
        input.ResumeRevisionId = cvRevision.Id; input.JobRevisionId = jdRevision.Id;
        input.Payload = PairMatchingContract.Serialize(PairMatchingContract.Build(cvRevision, jdRevision, version, request.Context == "public_pool"));
        input.Fingerprint = PairMatchingContract.Hash($"{cv.Id}:{cv.DocumentHash}:{cvRevision.Id}:{cvRevision.DataHash}:{version.Id}:{version.ContentHash}:{jdRevision.Id}:{jdRevision.DataHash}:{input.Payload}");
        return await jobs.EnqueueMatch(uid, request.IdempotencyKey, input, ct);
    }

    public async Task<PairJobView?> Read(Guid uid, Guid id, CancellationToken ct)
    {
        var job = await db.AiJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.RequestedBy == uid && x.JobType == "match_pair", ct);
        var input = await db.MatchInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == id, ct);
        if (job is null || input is null || !await CanAccess(db, uid, input, clock.GetUtcNow(), ct)) return null;
        var method = await db.AiMethods.AsNoTracking().SingleAsync(x => x.Id == job.MethodVersionId, ct);
        var result = await db.MatchResults.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == id, ct);
        var payload = JsonSerializer.Deserialize<PairPayload>(input.Payload, OutputValidator.Json)!;
        return new(job.Id, job.Status, job.ErrorCode, method.Provider, method.ConfigHash, payload.Rubric, input.Context,
            input.ResumeVersionId, input.ResumeRevisionId, input.JobVersionId, input.JobRevisionId, input.ApplicationId,
            result?.Score, result is null ? null : JsonSerializer.Deserialize<ScoredCriterion[]>(result.Criteria, OutputValidator.Json),
            payload.Location, payload.WorkMode, payload.SalaryMin, payload.SalaryMax, payload.SalaryCurrency, payload.SalaryPeriod);
    }

    public static async Task<bool> CanAccess(AppDb db, Guid uid, MatchInput input, DateTimeOffset now, CancellationToken ct, bool checkRevisions = true)
    {
        if (!await db.Users.AnyAsync(x => x.Id == uid && x.AccountStatus == "active", ct) ||
            !await db.Users.AnyAsync(x => x.Id == input.CandidateId && x.AccountStatus == "active", ct)) return false;
        var version = await (from v in db.ResumeVersions.AsNoTracking() join f in db.StoredFiles.AsNoTracking() on v.FileId equals f.Id
            where v.Id == input.ResumeVersionId && v.CandidateId == input.CandidateId && f.OwnerUserId == input.CandidateId &&
                f.Status == "ready" && f.Sha256 == v.DocumentHash select v).SingleOrDefaultAsync(ct);
        var job = await (from v in db.JobVersions.AsNoTracking() join j in db.Jobs.AsNoTracking() on v.JobId equals j.Id
            join c in db.Companies.AsNoTracking() on j.CompanyId equals c.Id
            where v.Id == input.JobVersionId && c.ArchivedAt == null && db.Users.Any(x => x.Id == c.OwnerUserId && x.AccountStatus == "active")
            select new { Version = v, Job = j, Owner = c.OwnerUserId }).SingleOrDefaultAsync(ct);
        if (version is null || job is null) return false;
        var role = input.Context == "self" ? "Candidate" : "Recruiter";
        if (!await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ur.UserId == uid && r.Name == role select ur.UserId).AnyAsync(ct)) return false;
        ApplicationRecord? application = null;
        if (input.Context == "application")
        {
            application = await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.ApplicationId &&
                x.CandidateId == input.CandidateId && x.SubmittedResumeVersionId == input.ResumeVersionId &&
                x.SubmittedJobVersionId == input.JobVersionId && x.JobId == job.Job.Id, ct);
            if (application is null || job.Owner != uid ||
                (application.Status is "rejected" or "withdrawn" && application.StatusChangedAt <= now.AddDays(-90))) return false;
        }
        else
        {
            if (job.Job.CurrentVersionId != input.JobVersionId || job.Job.Status != "published" || job.Job.ApplicationDeadline <= now) return false;
            if (input.Context == "self" && input.CandidateId != uid) return false;
            if (input.Context == "public_pool" && (job.Owner != uid || !await db.CandidatePublicResumes.AnyAsync(x =>
                x.CandidateId == input.CandidateId && x.ResumeVersionId == input.ResumeVersionId && x.Status == "ready" && x.SourceHash == version.DocumentHash, ct))) return false;
            if (input.Context is not ("self" or "public_pool")) return false;
        }
        if (!checkRevisions) return true;
        return await db.ResumeDataRevisions.AnyAsync(x => x.Id == input.ResumeRevisionId && x.ResumeVersionId == input.ResumeVersionId &&
            x.CandidateId == input.CandidateId && x.ConfirmedBy == input.CandidateId && x.ConfirmedAt != null &&
            (x.Origin == "user_confirmed" || x.Origin == "user_entered" && x.SchemaVersion == 2) &&
            (application == null || x.CreatedAt <= application.SubmittedAt && x.ConfirmedAt <= application.SubmittedAt), ct) &&
            await db.JobDataRevisions.AnyAsync(x => x.Id == input.JobRevisionId && x.JobVersionId == input.JobVersionId &&
                x.Origin == "user_confirmed" && x.ConfirmedBy == job.Owner && x.ConfirmedAt != null &&
                (application == null || x.CreatedAt <= application.SubmittedAt && x.ConfirmedAt <= application.SubmittedAt), ct);
    }
}

public static class PairMatchingEndpoints
{
    public static void MapPairMatching(this WebApplication app)
    {
        app.MapPost("/api/matching/pairs", async (CreatePairRequest request, ClaimsPrincipal user, PairMatchingService service, CancellationToken ct) =>
        {
            try
            {
                var job = await service.Enqueue(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), request, ct);
                return Results.Accepted($"/api/matching/pairs/{job.Id}", new { job.Id, job.Status });
            }
            catch (JobError e) { return Results.Problem(statusCode: e.Status, title: e.Code); }
        }).RequireAuthorization().RequireRateLimiting("matching");
        app.MapGet("/api/matching/pairs/{id:guid}", async (Guid id, ClaimsPrincipal user, PairMatchingService service, CancellationToken ct) =>
        {
            var value = await service.Read(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), id, ct);
            return value is null ? Results.NotFound() : Results.Ok(value);
        }).RequireAuthorization();
        app.MapGet("/api/matching/pairs/{id:guid}/evaluation-capture", async (Guid id, ClaimsPrincipal user, PairMatchingService service, AppDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var view = await service.Read(uid, id, ct);
            if (view is null) return Results.NotFound();
            if (view.Status is not ("succeeded" or "failed")) return Results.Problem(statusCode: 409, title: "capture_not_terminal");
            var input = await db.MatchInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == id, ct);
            if (input is null) return Results.NotFound();
            var job = await db.AiJobs.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            var method = await db.AiMethods.AsNoTracking().SingleAsync(x => x.Id == job.MethodVersionId, ct);
            var calls = await db.ExternalCallAttempts.AsNoTracking().Where(x => x.AiJobId == id).ToListAsync(ct);
            if (!await PairMatchingService.CanAccess(db, uid, input, clock.GetUtcNow(), ct)) return Results.NotFound();
            return Results.Ok(new { schemaVersion = 1, contract = PairMatchingContract.Version, view, input.Fingerprint,
                payload = JsonSerializer.Deserialize<JsonElement>(input.Payload), job.OutputJson,
                method = new { method.ConfigHash, method.Provider, method.Model, method.PromptVersion, method.SchemaVersion,
                    parameters = JsonSerializer.Deserialize<JsonElement>(method.Parameters), promptHash = PairMatchingContract.Hash(PairMatchingContract.Prompt) },
                usage = new { calls = calls.Count, cacheHit = calls.Count == 0 && job.Status == "succeeded",
                    unit = method.Provider == "Fake" ? "test_credit" : "usd",
                    inputTokens = calls.All(x => x.InputTokens.HasValue) ? (long?)calls.Sum(x => x.InputTokens!.Value) : null,
                    outputTokens = calls.All(x => x.OutputTokens.HasValue) ? (long?)calls.Sum(x => x.OutputTokens!.Value) : null,
                    amount = calls.All(x => x.ActualAmount.HasValue) ? (decimal?)calls.Sum(x => x.ActualAmount!.Value) : null,
                    unknownCalls = calls.Count(x => x.ReconciledAt == null),
                    elapsedMs = job.FinishedAt.HasValue ? (long?)(job.FinishedAt.Value - job.CreatedAt).TotalMilliseconds : null } });
        }).RequireAuthorization();
    }
}
