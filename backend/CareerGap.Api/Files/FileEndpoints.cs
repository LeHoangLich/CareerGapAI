using System.Security.Claims;
using System.Security.Cryptography;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Files;

public sealed record ResumeRenameRequest(long RowVersion, string? Title);
public sealed record ResumeVersionDto(Guid Id, Guid FileId, int VersionNo, string DocumentHash, DateTimeOffset CreatedAt);
public sealed record ResumeDto(Guid Id, string Title, long RowVersion, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<ResumeVersionDto> Versions);

public static class FileEndpoints
{
    public static void MapFiles(this WebApplication app)
    {
        app.MapPost("/api/resumes", async (HttpContext http, AppDb db, IFileStorage storage, DocumentParser parser, ILoggerFactory loggerFactory, CancellationToken ct) =>
            await UploadAsync(null, http, db, storage, parser, loggerFactory.CreateLogger("ResumeUpload"), ct))
            .RequireAuthorization().Accepts<IFormFile>("multipart/form-data");

        app.MapPost("/api/resumes/{resumeId:guid}/versions", async (Guid resumeId, HttpContext http, AppDb db,
            IFileStorage storage, DocumentParser parser, ILoggerFactory loggerFactory, CancellationToken ct) =>
            await UploadAsync(resumeId, http, db, storage, parser, loggerFactory.CreateLogger("ResumeUpload"), ct))
            .RequireAuthorization().Accepts<IFormFile>("multipart/form-data");

        app.MapPut("/api/resumes/{id:guid}", RenameResumeAsync).RequireAuthorization();
        app.MapGet("/api/resumes", GetResumesAsync).RequireAuthorization();

        app.MapGet("/api/files/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDb db, IFileStorage storage, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (!TryUserId(user, out var uid)) return Results.Unauthorized();
            var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == uid && x.Status == "ready", ct);
            return file is null ? Results.NotFound() : await ServeFileAsync(file, storage, ct, loggerFactory.CreateLogger("FileDownload"));
        }).RequireAuthorization();
    }

    internal static async Task<IResult> ServeFileAsync(StoredFile file, IFileStorage storage, CancellationToken ct, ILogger? logger = null)
    {
        if (file.Provider != storage.Provider)
        {
            logger?.LogWarning("ServeFileAsync provider mismatch: file={FileProvider}, active={ActiveProvider}", file.Provider, storage.Provider);
            return Results.Problem(statusCode: 503, title: "storage_unavailable");
        }
        try
        {
            var bytes = await storage.ReadAsync(file.ObjectKey, ct);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.Sha256)
                return Results.Problem(statusCode: 503, title: "file_unavailable");
            return Results.File(bytes, file.MimeType, file.OriginalName);
        }
        catch (StorageConfigurationException ex)
        {
            logger?.LogError(ex, "ServeFileAsync storage misconfigured (fileId={FileId}, provider={Provider}): {Message}", file.Id, storage.Provider, ex.Message);
            return Results.Problem(statusCode: 503, title: ex.Code);
        }
        catch (Exception ex) when (ex is IOException or Amazon.Runtime.AmazonServiceException or Amazon.Runtime.AmazonClientException or HttpRequestException or InvalidDataException)
        {
            logger?.LogError(ex, "ServeFileAsync failed (fileId={FileId}, provider={Provider}): {Type} {Message}", file.Id, storage.Provider, ex.GetType().Name, ex.Message);
            return Results.Problem(statusCode: 503, title: "file_unavailable");
        }
    }

    public static bool IsCompatibleUploadMime(string? contentType, string parsedMime)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return true;
        var normalized = contentType.Split(';', 2)[0].Trim();
        if (normalized.Length == 0 ||
            string.Equals(normalized, "application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, parsedMime, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (parsedMime == "application/pdf" &&
            string.Equals(normalized, "application/x-pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (parsedMime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" &&
            (string.Equals(normalized, "application/zip", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(normalized, "application/x-zip-compressed", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(normalized, "application/msword", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }
        return false;
    }

    private static async Task<IResult> UploadAsync(Guid? targetResumeId, HttpContext http, AppDb db,
        IFileStorage storage, DocumentParser parser, ILogger logger, CancellationToken ct)
    {
        if (!TryUserId(http.User, out var uid)) return Results.Unauthorized();
        if (!http.User.IsInRole("Candidate")) return Results.Forbid();
        if (targetResumeId is { } resumeId && !await db.Resumes.AsNoTracking().AnyAsync(x => x.Id == resumeId && x.CandidateId == uid, ct))
            return Results.NotFound();

        var form = await http.Request.ReadFormAsync(ct);
        var upload = form.Files.GetFile("file");
        if (upload is null || form.Files.Count != 1 || upload.Length is 0 or > DocumentParser.MaxBytes)
            return Results.Problem(statusCode: 400, title: "file_size");
        var name = Path.GetFileName(upload.FileName);
        if (name.Length is 0 or > 200) return Results.Problem(statusCode: 400, title: "file_name");
        using var buffer = new MemoryStream();
        await upload.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        ParsedDocument parsed;
        try { parsed = parser.Parse(bytes, name, ct); }
        catch (DocumentError ex)
        {
            logger.LogWarning("Resume parse failed ({Code}) for {Ext}", ex.Code, Path.GetExtension(name));
            return Results.Problem(statusCode: 422, title: ex.Code);
        }
        if (!IsCompatibleUploadMime(upload.ContentType, parsed.MimeType))
        {
            logger.LogWarning("Resume upload MIME mismatch: contentType={ContentType}, parsed={ParsedMime}", upload.ContentType, parsed.MimeType);
            return Results.Problem(statusCode: 422, title: "file_format");
        }

        // Store the upload under a fresh key before the short database transaction; never overwrite a file referenced by an older version.
        var file = new StoredFile { OwnerUserId = uid, ObjectKey = Guid.NewGuid().ToString("N"), Provider = storage.Provider,
            OriginalName = name, MimeType = parsed.MimeType, SizeBytes = bytes.Length };
        db.StoredFiles.Add(file);
        await db.SaveChangesAsync(ct);
        byte[] stored;
        try
        {
            await storage.PutAsync(file.ObjectKey, bytes, file.MimeType, ct);
            stored = await storage.ReadAsync(file.ObjectKey, ct);
        }
        catch (StorageConfigurationException ex)
        {
            logger.LogError(ex, "Resume upload storage misconfigured ({Provider}): {Message}", storage.Provider, ex.Message);
            await MarkFileFailedAsync(db, file);
            return Results.Problem(statusCode: 503, title: ex.Code);
        }
        catch (Exception ex) when (ex is IOException or Amazon.Runtime.AmazonServiceException or Amazon.Runtime.AmazonClientException or HttpRequestException or InvalidDataException
            || (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            logger.LogError(ex, "Resume storage verification failed ({Provider}): {Type} {Message}", storage.Provider, ex.GetType().Name, ex.Message);
            await MarkFileFailedAsync(db, file);
            return Results.Problem(statusCode: 503, title: "storage_verification_failed");
        }
        var sourceHash = SHA256.HashData(bytes);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(stored), sourceHash))
        {
            await MarkFileFailedAsync(db, file);
            return Results.Problem(statusCode: 503, title: "storage_verification_failed");
        }

        var hash = Convert.ToHexStringLower(sourceHash);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var uploadSlot = await db.StoredFiles.FromSqlInterpolated(
            $"SELECT * FROM stored_files WHERE id = {file.Id} AND owner_user_id = {uid} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        if (uploadSlot?.Status != "pending")
            return Results.Problem(statusCode: 503, title: "upload_cleanup_conflict");
        Resume resume;
        int previousVersion;
        if (targetResumeId is { } existingResumeId)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM resumes WHERE id = {existingResumeId} AND candidate_id = {uid} FOR UPDATE", ct);
            var existing = await db.Resumes.SingleOrDefaultAsync(x => x.Id == existingResumeId && x.CandidateId == uid, ct);
            if (existing is null) return Results.NotFound();
            resume = existing;
            previousVersion = await db.ResumeVersions.Where(x => x.ResumeId == resume.Id)
                .Select(x => (int?)x.VersionNo).MaxAsync(ct) ?? 0;
            if (previousVersion == int.MaxValue) return Results.Problem(statusCode: 409, title: "resume_version_limit");
            resume.RowVersion++;
            resume.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            resume = new Resume { CandidateId = uid, Title = name, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            previousVersion = 0;
            db.Resumes.Add(resume);
        }

        file.Sha256 = hash;
        file.Status = "ready";
        file.VerifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        var version = new ResumeVersion { ResumeId = resume.Id, CandidateId = uid, FileId = file.Id,
            VersionNo = previousVersion + 1, DocumentHash = hash };
        db.ResumeVersions.Add(version);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/files/{file.Id}", new
        {
            id = resume.Id, versionId = version.Id, fileId = file.Id, title = resume.Title,
            versionNo = version.VersionNo, rowVersion = resume.RowVersion
        });
    }

    private static async Task<IResult> RenameResumeAsync(Guid id, ResumeRenameRequest input, ClaimsPrincipal user,
        AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var uid)) return Results.Unauthorized();
        if (!user.IsInRole("Candidate")) return Results.Forbid();
        var title = string.IsNullOrWhiteSpace(input.Title) ? null : input.Title.Trim();
        if (title is null || title.Length > 200 || input.RowVersion < 0)
            return Results.Problem(statusCode: 400, title: "invalid_resume");
        var resume = await db.Resumes.SingleOrDefaultAsync(x => x.Id == id && x.CandidateId == uid, ct);
        if (resume is null) return Results.NotFound();
        if (resume.RowVersion != input.RowVersion) return Results.Problem(statusCode: 409, title: "resume_conflict");
        resume.Title = title;
        resume.RowVersion++;
        resume.UpdatedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Problem(statusCode: 409, title: "resume_conflict"); }
        return Results.Ok(new { resume.Id, resume.Title, resume.RowVersion, resume.CreatedAt, resume.UpdatedAt });
    }

    private static async Task<IResult> GetResumesAsync(ClaimsPrincipal user, AppDb db, int? page, CancellationToken ct)
    {
        if (!TryUserId(user, out var uid)) return Results.Unauthorized();
        if (!user.IsInRole("Candidate")) return Results.Forbid();
        var number = page ?? 1;
        if (number is < 1 or > 10000) return Results.Problem(statusCode: 400, title: "invalid_pagination");
        var total = await db.Resumes.AsNoTracking().CountAsync(x => x.CandidateId == uid, ct);
        var resumes = await db.Resumes.AsNoTracking().Where(x => x.CandidateId == uid)
            .OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Skip((number - 1) * 20).Take(20)
            .Select(x => new { x.Id, x.Title, x.RowVersion, x.CreatedAt, x.UpdatedAt }).ToListAsync(ct);
        var resumeIds = resumes.Select(x => x.Id).ToArray();
        var versions = await db.ResumeVersions.AsNoTracking().Where(x => x.CandidateId == uid && resumeIds.Contains(x.ResumeId))
            .OrderByDescending(x => x.VersionNo)
            .Select(x => new { x.Id, x.ResumeId, x.FileId, x.VersionNo, x.DocumentHash, x.CreatedAt }).ToListAsync(ct);
        var result = resumes.Select(resume => new ResumeDto(resume.Id, resume.Title, resume.RowVersion, resume.CreatedAt, resume.UpdatedAt,
            versions.Where(version => version.ResumeId == resume.Id)
                .Select(version => new ResumeVersionDto(version.Id, version.FileId, version.VersionNo, version.DocumentHash, version.CreatedAt)).ToArray()))
            .ToArray();
        return Results.Ok(new { items = result, total, page = number, pageSize = 20 });
    }

    private static async Task MarkFileFailedAsync(AppDb db, StoredFile file)
    {
        try
        {
            file.Status = "failed";
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch
        {
            // OrphanFileCleanupWorker still collects abandoned pending records after the grace period.
        }
    }

    private static bool TryUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
