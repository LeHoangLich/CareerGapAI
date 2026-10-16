using System.Security.Claims;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Recruitment;

public sealed record JobDataRevisionDto(Guid Id, Guid JobVersionId, int RevisionNo, Guid? ParentRevisionId,
    string Origin, int SchemaVersion, JsonElement StructuredData, JsonElement SourceText, JsonElement Evidence,
    string DataHash, string ParserVersion, Guid? ConfirmedBy, DateTimeOffset? ConfirmedAt, DateTimeOffset CreatedAt);

public static class JobRevisionEndpoints
{
    public static void MapJobRevisions(this WebApplication app)
    {
        var recruiterJobs = app.MapGroup("/api/recruiter/jobs").RequireAuthorization(policy => policy.RequireRole("Recruiter"));
        recruiterJobs.MapGet("/{jobId:guid}/versions/{versionId:guid}/revisions", GetAsync);
        recruiterJobs.MapPost("/{jobId:guid}/versions/{versionId:guid}/revisions/extract", ExtractAsync);
        recruiterJobs.MapPost("/{jobId:guid}/versions/{versionId:guid}/revisions/{revisionId:guid}/confirm", ConfirmAsync);
    }

    private static async Task<IResult> GetAsync(Guid jobId, Guid versionId, ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var ownerId)) return Results.Unauthorized();
        if (!await OwnsVersionAsync(db, jobId, versionId, ownerId, ct)) return Results.NotFound();
        var revisions = await db.JobDataRevisions.AsNoTracking().Where(x => x.JobVersionId == versionId)
            .OrderByDescending(x => x.RevisionNo).ToArrayAsync(ct);
        return Results.Ok(revisions.Select(ToDto).ToArray());
    }

    private static async Task<IResult> ExtractAsync(Guid jobId, Guid versionId, ClaimsPrincipal user, AppDb db,
        TimeProvider clock, CancellationToken ct)
    {
        if (!TryUserId(user, out var ownerId)) return Results.Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id = {jobId} FOR UPDATE", ct);
        if (!await OwnsJobAsync(db, jobId, ownerId, ct)) return Results.NotFound();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM job_versions WHERE id = {versionId} AND job_id = {jobId} FOR UPDATE", ct);
        var version = await db.JobVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId && x.JobId == jobId, ct);
        if (version is null) return Results.NotFound();

        var latest = await db.JobDataRevisions.Where(x => x.JobVersionId == versionId)
            .OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
        if (latest is not null && latest.ParserVersion == JobRevisionFactory.ParserVersion)
        {
            await transaction.CommitAsync(ct);
            return Results.Ok(ToDto(latest));
        }
        if (latest?.RevisionNo == int.MaxValue) return Results.Conflict(new { title = "job_revision_limit" });
        if (!JobRevisionFactory.TryExtract(version.Id, latest?.Id, (latest?.RevisionNo ?? 0) + 1,
                version.RequirementsText, clock.GetUtcNow(), out var revision, out var error))
            return Results.Problem(statusCode: 400, title: error);
        db.JobDataRevisions.Add(revision!);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/recruiter/jobs/{jobId}/versions/{versionId}/revisions", ToDto(revision!));
    }

    private static async Task<IResult> ConfirmAsync(Guid jobId, Guid versionId, Guid revisionId,
        ConfirmJobRevisionRequest? input, ClaimsPrincipal user, AppDb db, TimeProvider clock, CancellationToken ct)
    {
        if (!TryUserId(user, out var ownerId)) return Results.Unauthorized();
        if (input is null || input.BaseRevisionNo < 1 || input.AcceptedRequirements is null || input.UserStatements is null)
            return Results.Problem(statusCode: 400, title: "invalid_job_revision_confirmation");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id = {jobId} FOR UPDATE", ct);
        if (!await OwnsJobAsync(db, jobId, ownerId, ct)) return Results.NotFound();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM job_versions WHERE id = {versionId} AND job_id = {jobId} FOR UPDATE", ct);
        if (!await db.JobVersions.AnyAsync(x => x.Id == versionId && x.JobId == jobId, ct)) return Results.NotFound();
        var latest = await db.JobDataRevisions.AsNoTracking().Where(x => x.JobVersionId == versionId)
            .OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
        if (latest is null || latest.Id != revisionId || latest.RevisionNo != input.BaseRevisionNo)
            return Results.Conflict(new { title = "job_revision_conflict" });
        if (latest.RevisionNo == int.MaxValue) return Results.Conflict(new { title = "job_revision_limit" });
        if (!JobRevisionFactory.TryConfirm(latest, ownerId, latest.RevisionNo + 1, input,
                clock.GetUtcNow(), out var confirmed, out var error))
            return Results.Problem(statusCode: 400, title: error);
        db.JobDataRevisions.Add(confirmed!);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/recruiter/jobs/{jobId}/versions/{versionId}/revisions", ToDto(confirmed!));
    }

    private static async Task<bool> OwnsVersionAsync(AppDb db, Guid jobId, Guid versionId, Guid ownerId, CancellationToken ct) =>
        await db.JobVersions.AsNoTracking().AnyAsync(v => v.Id == versionId && v.JobId == jobId &&
            db.Jobs.Any(j => j.Id == v.JobId && j.Company!.OwnerUserId == ownerId), ct);

    private static Task<bool> OwnsJobAsync(AppDb db, Guid jobId, Guid ownerId, CancellationToken ct) =>
        db.Jobs.AsNoTracking().AnyAsync(x => x.Id == jobId && x.Company!.OwnerUserId == ownerId, ct);

    private static JobDataRevisionDto ToDto(JobDataRevision revision) => new(revision.Id, revision.JobVersionId,
        revision.RevisionNo, revision.ParentRevisionId, revision.Origin, revision.SchemaVersion,
        Parse(revision.StructuredData), Parse(revision.SourceText), Parse(revision.Evidence), revision.DataHash,
        revision.ParserVersion, revision.ConfirmedBy, revision.ConfirmedAt, revision.CreatedAt);

    private static JsonElement Parse(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static bool TryUserId(ClaimsPrincipal principal, out Guid userId) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
