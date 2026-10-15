using System.Security.Claims;
using System.Security.Cryptography;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Files;

public static class FileEndpoints
{
    public static void MapFiles(this WebApplication app)
    {
        app.MapPost("/api/resumes", async (HttpContext http, AppDb db, IFileStorage storage, DocumentParser parser, CancellationToken ct) =>
        {
            var uid = Guid.Parse(http.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!http.User.IsInRole("Candidate")) return Results.Forbid();
            var form = await http.Request.ReadFormAsync(ct);
            var upload = form.Files.GetFile("file");
            if (upload is null || form.Files.Count != 1 || upload.Length is 0 or > DocumentParser.MaxBytes) return Results.Problem(statusCode: 400, title: "file_size");
            var name = Path.GetFileName(upload.FileName);
            if (name.Length is 0 or > 200) return Results.Problem(statusCode: 400, title: "file_name");
            using var buffer = new MemoryStream();
            await upload.CopyToAsync(buffer, ct);
            var bytes = buffer.ToArray();
            ParsedDocument parsed;
            try { parsed = parser.Parse(bytes, name, ct); }
            catch (DocumentError ex) { return Results.Problem(statusCode: 422, title: ex.Code); }
            if (!string.IsNullOrWhiteSpace(upload.ContentType) && upload.ContentType != "application/octet-stream" && upload.ContentType != parsed.MimeType) return Results.Problem(statusCode: 422, title: "file_format");
            // Persist pending metadata first. Upload is outside a database transaction.
            var file = new StoredFile { OwnerUserId = uid, ObjectKey = Guid.NewGuid().ToString("N"), Provider = storage.Provider, OriginalName = name, MimeType = parsed.MimeType, SizeBytes = bytes.Length };
            db.StoredFiles.Add(file);
            await db.SaveChangesAsync(ct);
            await storage.PutAsync(file.ObjectKey, bytes, file.MimeType, ct);
            var stored = await storage.ReadAsync(file.ObjectKey, ct);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(stored), SHA256.HashData(bytes))) return Results.Problem(statusCode: 503, title: "storage_verification_failed");
            file.Sha256 = hash; file.Status = "ready"; file.VerifiedAt = DateTimeOffset.UtcNow;
            var resume = new Resume { CandidateId = uid, Title = name };
            var version = new ResumeVersion { ResumeId = resume.Id, CandidateId = uid, FileId = file.Id, DocumentHash = hash };
            db.Resumes.Add(resume); db.ResumeVersions.Add(version);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/files/{file.Id}", new { resume.Id, VersionId = version.Id, FileId = file.Id, Title = resume.Title });
        }).RequireAuthorization().Accepts<IFormFile>("multipart/form-data");

        app.MapGet("/api/resumes", async (ClaimsPrincipal user, AppDb db, int? page, CancellationToken ct) =>
        {
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var number = Math.Max(1, page ?? 1);
            if (number > 10000) return Results.BadRequest();
            var items = await (from v in db.ResumeVersions.AsNoTracking() join r in db.Resumes on v.ResumeId equals r.Id
                where v.CandidateId == uid orderby v.CreatedAt descending, v.Id
                select new { r.Id, VersionId = v.Id, v.FileId, r.Title, v.VersionNo }).Skip((number - 1) * 20).Take(20).ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        app.MapGet("/api/files/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDb db, IFileStorage storage, CancellationToken ct) =>
        {
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == uid && x.Status == "ready", ct);
            if (file is null) return Results.NotFound();
            if (file.Provider != storage.Provider) return Results.Problem(statusCode: 503, title: "storage_unavailable");
            try
            {
                var bytes = await storage.ReadAsync(file.ObjectKey, ct);
                if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != file.Sha256) return Results.Problem(statusCode: 503, title: "file_unavailable");
                return Results.File(bytes, file.MimeType, file.OriginalName);
            }
            catch (Exception ex) when (ex is IOException or Amazon.S3.AmazonS3Exception) { return Results.Problem(statusCode: 503, title: "file_unavailable"); }
        }).RequireAuthorization();
    }
}
