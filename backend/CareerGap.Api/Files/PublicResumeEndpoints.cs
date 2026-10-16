using System.Security.Claims;
using System.Security.Cryptography;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Files;

public sealed record PublishResumeRequest(Guid ResumeVersionId, long? ExpectedRowVersion);
public sealed record PublicResumeSelectionDto(Guid CandidateId, Guid ResumeVersionId, int VersionNo, string Status,
    long RowVersion, DateTimeOffset PublishedAt, DateTimeOffset? RedactionVerifiedAt);
public sealed record PublicResumeListItemDto(Guid CandidateId, Guid ResumeVersionId, string DisplayName, string? Headline, int VersionNo,
    DateTimeOffset PublishedAt, string RedactionStatus);

public static class PublicResumeEndpoints
{
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static void MapPublicResumeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/resumes/public/me", GetMineAsync).RequireAuthorization(policy => policy.RequireRole("Candidate"));
        app.MapPost("/api/resumes/public", PublishAsync).RequireAuthorization(policy => policy.RequireRole("Candidate"));
        app.MapDelete("/api/resumes/public/me", HideAsync).RequireAuthorization(policy => policy.RequireRole("Candidate"));
        app.MapGet("/api/public/resumes", BrowseAsync).RequireAuthorization();
        app.MapGet("/api/public/resumes/{candidateId:guid}/{resumeVersionId:guid}/file", ReadPublicResumeAsync).RequireAuthorization();
    }

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var uid)) return Results.Unauthorized();
        var selection = await (from publication in db.CandidatePublicResumes.AsNoTracking()
            join version in db.ResumeVersions.AsNoTracking() on new { publication.ResumeVersionId, publication.CandidateId }
                equals new { ResumeVersionId = version.Id, version.CandidateId }
            where publication.CandidateId == uid
            select new PublicResumeSelectionDto(publication.CandidateId, version.Id, version.VersionNo,
                publication.Status, publication.RowVersion, publication.PublishedAt, publication.RedactionVerifiedAt))
            .SingleOrDefaultAsync(ct);
        return selection is null ? Results.Content("null", "application/json") : Results.Ok(selection);
    }

    private static async Task<IResult> PublishAsync(PublishResumeRequest input, ClaimsPrincipal user, AppDb db,
        IFileStorage storage, DocumentParser parser, RedactedResumeBuilder builder, CancellationToken ct)
    {
        if (!TryUserId(user, out var uid)) return Results.Unauthorized();
        if (input.ResumeVersionId == Guid.Empty || input.ExpectedRowVersion is < 0)
            return Results.Problem(statusCode: 400, title: "invalid_public_resume");

        var source = await (from version in db.ResumeVersions.AsNoTracking()
            join file in db.StoredFiles.AsNoTracking() on new { version.FileId, version.CandidateId }
                equals new { FileId = file.Id, CandidateId = file.OwnerUserId }
            where version.Id == input.ResumeVersionId && version.CandidateId == uid && file.Status == "ready"
            select new { Version = version, File = file }).SingleOrDefaultAsync(ct);
        if (source is null) return Results.NotFound();
        if (source.File.Provider != storage.Provider) return Results.Problem(statusCode: 503, title: "storage_unavailable");

        byte[] sourceBytes;
        try { sourceBytes = await storage.ReadAsync(source.File.ObjectKey, ct); }
        catch (Exception ex) when (ex is IOException or Amazon.S3.AmazonS3Exception)
        { return Results.Problem(statusCode: 503, title: "file_unavailable"); }
        var actualSourceHash = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
        if (source.File.Sha256 != actualSourceHash || source.Version.DocumentHash != actualSourceHash)
            return Results.Problem(statusCode: 503, title: "file_unavailable");

        byte[]? derivedBytes = null;
        try { derivedBytes = builder.Build(parser.Parse(sourceBytes, source.File.OriginalName, ct), ct); }
        catch (DocumentError) { /* Keep the publication recruiter-readable but unavailable to other candidates. */ }

        StoredFile? derivedFile = null;
        if (derivedBytes is not null)
        {
            var derivedHash = Convert.ToHexStringLower(SHA256.HashData(derivedBytes));
            derivedFile = new StoredFile
            {
                OwnerUserId = uid, ObjectKey = Guid.NewGuid().ToString("N"), Provider = source.File.Provider,
                OriginalName = "resume-redacted.docx", MimeType = DocxMime, SizeBytes = derivedBytes.Length,
                Sha256 = derivedHash, Status = "pending"
            };
            // Persist metadata first so any interruption after the object write leaves a recoverable pending row.
            db.StoredFiles.Add(derivedFile);
            await db.SaveChangesAsync(ct);
            try
            {
                await storage.PutAsync(derivedFile.ObjectKey, derivedBytes, DocxMime, ct);
                var storedBytes = await storage.ReadAsync(derivedFile.ObjectKey, ct);
                if (Convert.ToHexStringLower(SHA256.HashData(storedBytes)) != derivedHash || !builder.Verify(storedBytes, ct))
                    return Results.Problem(statusCode: 503, title: "redacted_file_unavailable");
            }
            catch (Exception ex) when (ex is IOException or Amazon.S3.AmazonS3Exception or InvalidDataException)
            {
                return Results.Problem(statusCode: 503, title: "redacted_file_unavailable");
            }
        }

        var committed = await CommitPublicationAsync(uid, input, source.Version, source.File, derivedFile,
            actualSourceHash, db, ct);
        return committed.Result;
    }

    private sealed record CommitResult(IResult Result, bool Saved);

    private static async Task<CommitResult> CommitPublicationAsync(Guid uid, PublishResumeRequest input,
        ResumeVersion sourceVersion, StoredFile sourceFile, StoredFile? derivedFile,
        string sourceHash, AppDb db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT user_id FROM candidate_profiles WHERE user_id = {uid} FOR UPDATE", ct);
        var current = await db.CandidatePublicResumes.SingleOrDefaultAsync(x => x.CandidateId == uid, ct);
        if (input.ExpectedRowVersion != current?.RowVersion)
            return new(Results.Conflict(new { title = "public_resume_conflict" }), false);

        var stillOwned = await db.ResumeVersions.AsNoTracking().AnyAsync(x => x.Id == sourceVersion.Id && x.CandidateId == uid &&
            x.DocumentHash == sourceHash && x.FileId == sourceFile.Id, ct);
        var stillReady = await db.StoredFiles.AsNoTracking().AnyAsync(x => x.Id == sourceFile.Id && x.OwnerUserId == uid &&
            x.Status == "ready" && x.Sha256 == sourceHash, ct);
        if (!stillOwned || !stillReady) return new(Results.NotFound(), false);

        var now = DateTimeOffset.UtcNow;
        if (derivedFile is not null)
        {
            var uploadSlot = await db.StoredFiles.FromSqlInterpolated(
                $"SELECT * FROM stored_files WHERE id = {derivedFile.Id} AND owner_user_id = {uid} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(ct);
            if (uploadSlot?.Status != "pending")
                return new(Results.Problem(statusCode: 503, title: "upload_cleanup_conflict"), false);
            derivedFile.Status = "ready";
            derivedFile.VerifiedAt = now;
        }

        if (current is null)
        {
            current = new CandidatePublicResume { CandidateId = uid, RowVersion = 0 };
            db.CandidatePublicResumes.Add(current);
        }
        else current.RowVersion++;
        current.ResumeVersionId = sourceVersion.Id;
        current.RedactedFileId = derivedFile?.Id;
        current.SourceHash = sourceHash;
        current.Status = derivedFile is null ? "not_ready" : "ready";
        current.RedactionMethodVersion = derivedFile is null ? null : RedactedResumeBuilder.MethodVersion;
        current.RedactionVerifiedAt = derivedFile is null ? null : now;
        current.PublishedAt = now;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var result = new PublicResumeSelectionDto(uid, sourceVersion.Id, sourceVersion.VersionNo, current.Status,
            current.RowVersion, current.PublishedAt, current.RedactionVerifiedAt);
        return new(Results.Ok(result), true);
    }

    private static async Task<IResult> HideAsync(long rowVersion, ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var uid)) return Results.Unauthorized();
        if (rowVersion < 0) return Results.Problem(statusCode: 400, title: "invalid_public_resume");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT user_id FROM candidate_profiles WHERE user_id = {uid} FOR UPDATE", ct);
        var selection = await db.CandidatePublicResumes.SingleOrDefaultAsync(x => x.CandidateId == uid, ct);
        if (selection is null) return Results.NotFound();
        if (selection.RowVersion != rowVersion) return Results.Conflict(new { title = "public_resume_conflict" });
        db.CandidatePublicResumes.Remove(selection);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> BrowseAsync(ClaimsPrincipal user, AppDb db, int? page, CancellationToken ct)
    {
        if (!TryUserId(user, out var uid)) return Results.Unauthorized();
        var isCandidate = user.IsInRole("Candidate");
        var isRecruiter = user.IsInRole("Recruiter");
        if (!isCandidate && !isRecruiter) return Results.Forbid();
        var number = page ?? 1;
        if (number is < 1 or > 10000) return Results.BadRequest();

        var query = from publication in db.CandidatePublicResumes.AsNoTracking()
            join version in db.ResumeVersions.AsNoTracking() on new { publication.ResumeVersionId, publication.CandidateId }
                equals new { ResumeVersionId = version.Id, version.CandidateId }
            join profile in db.CandidateProfiles.AsNoTracking() on publication.CandidateId equals profile.UserId
            where publication.CandidateId != uid && db.Users.Any(account => account.Id == publication.CandidateId && account.AccountStatus == "active") &&
                (isRecruiter || publication.Status == "ready")
            orderby publication.PublishedAt descending, publication.CandidateId
            select new PublicResumeListItemDto(publication.CandidateId, version.Id,
                db.CandidateProfileVisibility.Any(v => v.CandidateId == profile.UserId && v.FieldKey == "display_name") ? profile.DisplayName : "Ứng viên",
                db.CandidateProfileVisibility.Any(v => v.CandidateId == profile.UserId && v.FieldKey == "headline") ? profile.Headline : null,
                version.VersionNo, publication.PublishedAt, publication.Status);
        var results = await query.Skip((number - 1) * 20).Take(20).ToArrayAsync(ct);
        return Results.Ok(results);
    }

    private static async Task<IResult> ReadPublicResumeAsync(Guid candidateId, Guid resumeVersionId, ClaimsPrincipal user, AppDb db,
        IFileStorage storage, RedactedResumeBuilder builder, CancellationToken ct)
    {
        if (!TryUserId(user, out var uid)) return Results.Unauthorized();
        var isCandidate = user.IsInRole("Candidate");
        var isRecruiter = user.IsInRole("Recruiter");
        if (!isCandidate && !isRecruiter) return Results.Forbid();
        if (isCandidate && candidateId == uid) return Results.NotFound();
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == candidateId && x.AccountStatus == "active", ct)) return Results.NotFound();

        var selection = await db.CandidatePublicResumes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CandidateId == candidateId && x.ResumeVersionId == resumeVersionId, ct);
        if (selection is null) return Results.NotFound();
        var source = await (from version in db.ResumeVersions.AsNoTracking()
            join file in db.StoredFiles.AsNoTracking() on new { version.FileId, version.CandidateId }
                equals new { FileId = file.Id, CandidateId = file.OwnerUserId }
            where version.Id == selection.ResumeVersionId && version.CandidateId == candidateId
            select new { Version = version, File = file }).SingleOrDefaultAsync(ct);
        if (source is null || source.File.Status != "ready" || source.Version.DocumentHash != selection.SourceHash || source.File.Sha256 != selection.SourceHash)
            return Results.NotFound();

        if (isRecruiter) return await FileEndpoints.ServeFileAsync(source.File, storage, ct);
        if (selection.Status != "ready" || selection.RedactedFileId is null ||
            selection.RedactionMethodVersion != RedactedResumeBuilder.MethodVersion || selection.RedactionVerifiedAt is null)
            return Results.Conflict(new { title = "redaction_not_ready" });

        var redacted = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == selection.RedactedFileId &&
            x.OwnerUserId == candidateId && x.Status == "ready", ct);
        if (redacted is null || redacted.Provider != storage.Provider || redacted.Sha256 is null) return Results.NotFound();
        try
        {
            var bytes = await storage.ReadAsync(redacted.ObjectKey, ct);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != redacted.Sha256 || !builder.Verify(bytes, ct))
                return Results.Problem(statusCode: 503, title: "redacted_file_unavailable");
            return Results.File(bytes, DocxMime, "resume-redacted.docx");
        }
        catch (Exception ex) when (ex is IOException or Amazon.S3.AmazonS3Exception or InvalidDataException)
        { return Results.Problem(statusCode: 503, title: "redacted_file_unavailable"); }
    }

    private static bool TryUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
