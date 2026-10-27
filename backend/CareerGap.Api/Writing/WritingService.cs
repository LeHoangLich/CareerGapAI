using System.Security.Claims;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Matching;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Writing;

public sealed record CreateWritingRequest(Guid ResumeVersionId, Guid IdempotencyKey, string Language = "vi");
public sealed record WritingDecisionRequest(string Action, IReadOnlyList<WritingEdit>? Sections = null);
public sealed class WritingService(AppDb db, JobService jobs)
{
    public static Task<bool> CanAccess(AppDb db, Guid uid, WritingInput input, CancellationToken ct) =>
        (from v in db.ResumeVersions join f in db.StoredFiles on v.FileId equals f.Id
         join r in db.ResumeDataRevisions on input.ResumeRevisionId equals r.Id
         where v.Id == input.ResumeVersionId && v.CandidateId == uid && input.CandidateId == uid && f.OwnerUserId == uid && f.Status == "ready" && f.Sha256 == v.DocumentHash &&
             r.ResumeVersionId == v.Id && r.CandidateId == uid && r.ConfirmedBy == uid && r.ConfirmedAt != null &&
             (r.Origin == "user_confirmed" || r.Origin == "user_entered" && r.SchemaVersion == 2) &&
             db.Users.Any(u => u.Id == uid && u.AccountStatus == "active") &&
             (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId == uid && role.Name == "Candidate" select ur).Any()
         select v.Id).AnyAsync(ct);
    public async Task<AiJob> Enqueue(Guid uid, CreateWritingRequest request, CancellationToken ct)
    {
        var previous = await db.AiJobs.SingleOrDefaultAsync(x => x.RequestedBy == uid && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (previous is not null)
        {
            var pinned = await db.WritingInputs.SingleOrDefaultAsync(x => x.AiJobId == previous.Id, ct);
            if (pinned is null || pinned.ResumeVersionId != request.ResumeVersionId || JsonSerializer.Deserialize<WritingPayload>(pinned.Payload, OutputValidator.Json)!.Language != request.Language) throw new JobError("idempotency_conflict", 409);
            if (!await CanAccess(db, uid, pinned, ct)) throw new JobError("not_found", 404);
            return previous;
        }
        if (!await db.ResumeVersions.AnyAsync(x => x.Id == request.ResumeVersionId && x.CandidateId == uid, ct)) throw new JobError("not_found", 404);
        var revision = await db.ResumeDataRevisions.AsNoTracking().Where(x => x.ResumeVersionId == request.ResumeVersionId && x.CandidateId == uid && x.ConfirmedBy == uid && x.ConfirmedAt != null &&
            (x.Origin == "user_confirmed" || x.Origin == "user_entered" && x.SchemaVersion == 2)).OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct) ?? throw new JobError("confirmed_resume_required", 409);
        var payload = PairMatchingContract.Serialize(WritingContract.Build(revision, request.Language));
        var input = new WritingInput { CandidateId = uid, ResumeVersionId = revision.ResumeVersionId, ResumeRevisionId = revision.Id,
            Payload = payload, Fingerprint = PairMatchingContract.Hash($"{revision.ResumeVersionId}:{revision.Id}:{revision.DataHash}:{payload}") };
        if (!await CanAccess(db, uid, input, ct)) throw new JobError("not_found", 404);
        return await jobs.EnqueueWriting(uid, request.IdempotencyKey, input, ct);
    }
    public async Task<object?> Read(Guid uid, Guid id, CancellationToken ct)
    {
        var input = await db.WritingInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == id, ct);
        if (input is null || !await CanAccess(db, uid, input, ct)) return null;
        var job = await db.AiJobs.AsNoTracking().SingleAsync(x => x.Id == id && x.RequestedBy == uid, ct);
        var result = await db.WritingResults.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == id, ct);
        var decision = await db.WritingDecisions.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == id, ct);
        var provider = await db.AiMethods.Where(x => x.Id == job.MethodVersionId).Select(x => x.Provider).SingleAsync(ct);
        return new { job.Id, job.Status, job.ErrorCode, provider, input.ResumeVersionId, input.ResumeRevisionId,
            sections = result is null ? null : JsonSerializer.Deserialize<WritingSection[]>(result.Sections, OutputValidator.Json),
            decision = decision?.Action, acceptedSections = decision?.Action == "accept" ? JsonSerializer.Deserialize<WritingEdit[]>(decision.Sections, OutputValidator.Json) : null };
    }
    public async Task<object> Decide(Guid uid, Guid id, WritingDecisionRequest request, CancellationToken ct)
    {
        if (request.Action is not ("accept" or "discard")) throw new JobError("invalid_writing_decision", 400);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var job = await db.AiJobs.FromSqlInterpolated($"SELECT * FROM ai_jobs WHERE id = {id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        var input = await db.WritingInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == id, ct);
        if (job is null || input is null || job.RequestedBy != uid || !await CanAccess(db, uid, input, ct)) throw new JobError("not_found", 404);
        if (job.Status != "succeeded") throw new JobError("writing_not_ready", 409);
        var result = await db.WritingResults.AsNoTracking().SingleAsync(x => x.AiJobId == id, ct);
        var original = JsonSerializer.Deserialize<WritingSection[]>(result.Sections, OutputValidator.Json)!;
        var edits = request.Action == "discard" ? [] : request.Sections ?? original.Select(x => new WritingEdit(x.Target, x.Text)).ToArray();
        if (edits.Count > original.Length || edits.Any(x => x is null || !original.Any(o => o.Target == x.Target) || string.IsNullOrWhiteSpace(x.Text) || x.Text.Length > (x.Target == "summary" ? 3000 : 1200)) || edits.Select(x => x.Target).Distinct().Count() != edits.Count || request.Action == "accept" && edits.Count == 0)
            throw new JobError("invalid_writing_decision", 400);
        var serialized = PairMatchingContract.Serialize(edits);
        var existing = await db.WritingDecisions.SingleOrDefaultAsync(x => x.AiJobId == id, ct);
        if (existing is not null && (existing.Action != request.Action || !JsonSerializer.Deserialize<WritingEdit[]>(existing.Sections, OutputValidator.Json)!.SequenceEqual(edits))) throw new JobError("writing_already_decided", 409);
        if (existing is null) { db.WritingDecisions.Add(new WritingDecision { AiJobId = id, Action = request.Action, Sections = serialized }); await db.SaveChangesAsync(ct); }
        await transaction.CommitAsync(ct);
        return new { action = request.Action, sections = edits, sourceType = "user_statement", savedCvVersion = false };
    }
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/writing/proposals", async (Guid resumeVersionId, ClaimsPrincipal user, AppDb db, WritingService service, CancellationToken ct) =>
        {
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!await db.ResumeVersions.AnyAsync(x => x.Id == resumeVersionId && x.CandidateId == uid, ct)) return Results.NotFound();
            var id = await db.WritingInputs.Where(x => x.CandidateId == uid && x.ResumeVersionId == resumeVersionId).Join(db.AiJobs, x => x.AiJobId, x => x.Id, (i,j) => j)
                .OrderByDescending(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
            return Results.Ok(new { proposal = id is null ? null : await service.Read(uid, id.Value, ct) });
        }).RequireAuthorization(p => p.RequireRole("Candidate"));
        app.MapPost("/api/writing/proposals", async (CreateWritingRequest input, ClaimsPrincipal user, WritingService service, CancellationToken ct) =>
        {
            try { var job = await service.Enqueue(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), input, ct); return Results.Accepted($"/api/writing/proposals/{job.Id}", new { job.Id, job.Status }); }
            catch (JobError e) { return Results.Problem(statusCode: e.Status, title: e.Code); }
        }).RequireAuthorization(p => p.RequireRole("Candidate")).RequireRateLimiting("matching");
        app.MapGet("/api/writing/proposals/{id:guid}", async (Guid id, ClaimsPrincipal user, WritingService service, CancellationToken ct) =>
        { var value = await service.Read(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), id, ct); return value is null ? Results.NotFound() : Results.Ok(value); }).RequireAuthorization();
        app.MapPost("/api/writing/proposals/{id:guid}/decision", async (Guid id, WritingDecisionRequest input, ClaimsPrincipal user, WritingService service, CancellationToken ct) =>
        { try { return Results.Ok(await service.Decide(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), id, input, ct)); } catch (JobError e) { return Results.Problem(statusCode: e.Status, title: e.Code); } }).RequireAuthorization(p => p.RequireRole("Candidate"));
    }
}
