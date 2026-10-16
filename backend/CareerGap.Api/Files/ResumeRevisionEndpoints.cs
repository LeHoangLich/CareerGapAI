using System.Security.Claims;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Files;

public sealed record ResumeDataRevisionDto(Guid Id, Guid ResumeVersionId, int RevisionNo, Guid? ParentRevisionId,
    string Origin, int SchemaVersion, JsonElement StructuredData, JsonElement SourceText, JsonElement Evidence,
    string DataHash, string ParserVersion, Guid? MethodVersionId, Guid? AiJobId, Guid? ConfirmedBy,
    DateTimeOffset? ConfirmedAt, DateTimeOffset CreatedAt);

public static class ResumeRevisionEndpoints
{
    public static void MapResumeRevisions(this WebApplication app)
    {
        app.MapGet("/api/resume-versions/{versionId:guid}/revisions", GetAsync).RequireAuthorization();
        app.MapPost("/api/resume-versions/{versionId:guid}/revisions/{revisionId:guid}/confirm", ConfirmAsync).RequireAuthorization();
        app.MapPost("/api/resume-versions/{versionId:guid}/revisions/manual", SaveBuilderRevisionAsync).RequireAuthorization();
    }

    private static async Task<IResult> GetAsync(Guid versionId, ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryCandidate(user, out var candidateId)) return Results.Forbid();
        if (!await db.ResumeVersions.AsNoTracking().AnyAsync(x => x.Id == versionId && x.CandidateId == candidateId, ct))
            return Results.NotFound();
        var revisions = await db.ResumeDataRevisions.AsNoTracking()
            .Where(x => x.ResumeVersionId == versionId && x.CandidateId == candidateId)
            .OrderByDescending(x => x.RevisionNo).ToArrayAsync(ct);
        return Results.Ok(revisions.Select(ToDto).ToArray());
    }

    private static async Task<IResult> ConfirmAsync(Guid versionId, Guid revisionId, ConfirmResumeRevisionRequest input,
        ClaimsPrincipal user, AppDb db, TimeProvider clock, CancellationToken ct)
    {
        if (!TryCandidate(user, out var candidateId)) return Results.Forbid();
        if (input.BaseRevisionNo < 1 || input.AcceptedSkillNames is null || input.UserStatementSkills is null)
            return Results.Problem(statusCode: 400, title: "invalid_revision_confirmation");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var version = await db.ResumeVersions.FromSqlInterpolated(
            $"SELECT * FROM resume_versions WHERE id = {versionId} AND candidate_id = {candidateId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (version is null) return Results.NotFound();
        var latest = await db.ResumeDataRevisions.AsNoTracking()
            .Where(x => x.ResumeVersionId == versionId && x.CandidateId == candidateId)
            .OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
        if (latest is null || latest.Id != revisionId || latest.RevisionNo != input.BaseRevisionNo)
            return Results.Conflict(new { title = "resume_revision_conflict" });

        if (!ResumeRevisionFactory.TryConfirm(latest, candidateId, latest.RevisionNo + 1, input,
                clock.GetUtcNow(), out var confirmed, out var error))
            return Results.Problem(statusCode: 400, title: error);

        db.ResumeDataRevisions.Add(confirmed!);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/resume-versions/{versionId}/revisions", ToDto(confirmed!));
    }

    private static async Task<IResult> SaveBuilderRevisionAsync(Guid versionId, ResumeBuilderContentRequest input,
        ClaimsPrincipal user, AppDb db, TimeProvider clock, CancellationToken ct)
    {
        if (!TryCandidate(user, out var candidateId)) return Results.Forbid();
        if (!ResumeBuilderRevisionFactory.TryPrepare(input, out var prepared, out var error))
            return Results.Problem(statusCode: 400, title: error);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var version = await db.ResumeVersions.FromSqlInterpolated(
            $"SELECT * FROM resume_versions WHERE id = {versionId} AND candidate_id = {candidateId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (version is null) return Results.NotFound();
        var latest = await db.ResumeDataRevisions.AsNoTracking()
            .Where(x => x.ResumeVersionId == versionId && x.CandidateId == candidateId)
            .OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);

        // A repeated save after a lost response is safe: identical latest content returns the existing immutable revision.
        if (latest is { Origin: "user_entered", SchemaVersion: ResumeBuilderRevisionFactory.SchemaVersion } &&
            latest.DataHash == prepared!.DataHash)
        {
            await transaction.CommitAsync(ct);
            return Results.Ok(ToDto(latest));
        }

        var currentRevisionNo = latest?.RevisionNo ?? 0;
        if (input.BaseRevisionNo != currentRevisionNo)
            return Results.Conflict(new { title = "resume_revision_conflict" });
        if (currentRevisionNo == int.MaxValue)
            return Results.Problem(statusCode: 409, title: "resume_revision_limit");

        var revision = ResumeBuilderRevisionFactory.Create(candidateId, versionId, latest?.Id,
            currentRevisionNo + 1, prepared!, clock.GetUtcNow());
        db.ResumeDataRevisions.Add(revision);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/resume-versions/{versionId}/revisions", ToDto(revision));
    }

    private static ResumeDataRevisionDto ToDto(ResumeDataRevision revision) => new(revision.Id,
        revision.ResumeVersionId, revision.RevisionNo, revision.ParentRevisionId, revision.Origin,
        revision.SchemaVersion, Parse(revision.StructuredData), Parse(revision.SourceText), Parse(revision.Evidence),
        revision.DataHash, revision.ParserVersion, revision.MethodVersionId, revision.AiJobId,
        revision.ConfirmedBy, revision.ConfirmedAt, revision.CreatedAt);

    private static JsonElement Parse(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static bool TryCandidate(ClaimsPrincipal user, out Guid candidateId)
    {
        candidateId = default;
        return user.IsInRole("Candidate") && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out candidateId);
    }
}
