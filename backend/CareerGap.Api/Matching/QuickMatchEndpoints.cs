using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Matching;

public sealed record JdUrlPreviewRequest(string? Url);
public sealed record QuickMatchRequest(
    string CvSource,
    Guid? ResumeVersionId,
    string? CvText,
    string JdSource,
    Guid? JobVersionId,
    string? JdTitle,
    string? JdText,
    string? JdUrl,
    Guid IdempotencyKey,
    string? CareerContext = null);

public static class QuickMatchEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void MapQuickMatch(this WebApplication app)
    {
        app.MapPost("/api/matching/jd-url-preview", async (JdUrlPreviewRequest input, JdUrlImporter importer, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Url))
                return Results.Problem(statusCode: 400, title: "invalid_jd_url");
            try
            {
                var result = await importer.FetchAsync(input.Url, ct);
                return Results.Ok(result);
            }
            catch (JdUrlImportException ex)
            {
                return Results.Problem(statusCode: ex.Status, title: ex.Code);
            }
        }).RequireAuthorization(p => p.RequireRole("Candidate")).RequireRateLimiting("matching");

        app.MapPost("/api/matching/quick-pairs", async (QuickMatchRequest input, ClaimsPrincipal user, AppDb db,
            IFileStorage storage, JobService jobs, TimeProvider clock, CancellationToken ct) =>
        {
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (input.IdempotencyKey == Guid.Empty)
                return Results.Problem(statusCode: 400, title: "idempotency_key_required");
            string? careerContext;
            try { careerContext = PairMatchingService.ValidateCareerContext(input.CareerContext); }
            catch (JobError e) { return Results.Problem(statusCode: e.Status, title: e.Code); }

            if (input.CvSource is not ("existing" or "paste") || input.JdSource is not ("platform" or "paste" or "url"))
                return Results.Problem(statusCode: 400, title: "invalid_quick_match_source");

            var previous = await db.AiJobs.AsNoTracking().SingleOrDefaultAsync(x => x.RequestedBy == uid && x.IdempotencyKey == input.IdempotencyKey, ct);
            if (previous is not null)
                return Results.Accepted($"/api/matching/pairs/{previous.Id}", new { previous.Id, previous.Status });

            var now = clock.GetUtcNow();
            Guid resumeVersionId;
            ResumeDataRevision cvRevision;

            if (input.CvSource == "existing")
            {
                if (input.ResumeVersionId is not { } rvid || rvid == Guid.Empty)
                    return Results.Problem(statusCode: 400, title: "resume_version_required");
                var cv = await db.ResumeVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == rvid && x.CandidateId == uid, ct);
                if (cv is null) return Results.NotFound();
                var confirmedCv = await db.ResumeDataRevisions.AsNoTracking().Where(x => x.ResumeVersionId == cv.Id && x.CandidateId == uid &&
                    (x.Origin == "user_confirmed" || x.Origin == "user_entered" && x.SchemaVersion == 2) && x.ConfirmedBy == uid && x.ConfirmedAt != null)
                    .OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
                if (confirmedCv is null) return Results.Problem(statusCode: 409, title: "confirmed_resume_required");
                cvRevision = confirmedCv;
                resumeVersionId = cv.Id;
            }
            else
            {
                var rawCv = input.CvText?.ReplaceLineEndings("\n").Trim() ?? "";
                if (rawCv.Length is < 20 or > 20000 || rawCv.Contains('\0'))
                    return Results.Problem(statusCode: 400, title: "invalid_quick_cv_text");

                var bytes = Encoding.UTF8.GetBytes(rawCv);
                var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
                var objectKey = Guid.NewGuid().ToString("N");
                var lines = rawCv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var headline = lines.FirstOrDefault() ?? "CV dán nhanh";
                if (headline.Length > 200) headline = headline[..200];
                var skills = SkillCatalog.Items
                    .Where(s => new[] { s.Name }.Concat(s.Aliases).Any(a => rawCv.Contains(a, StringComparison.OrdinalIgnoreCase)))
                    .Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
                var builderReq = new ResumeBuilderContentRequest(
                    0, ResumeBuilderRevisionFactory.TemplateVersion, "CV đối chiếu nhanh",
                    null, null, null, headline, rawCv.Length > 4000 ? rawCv[..4000] : rawCv,
                    skills, [], [], [], []);
                if (!ResumeBuilderRevisionFactory.TryPrepare(builderReq, out var prepared, out var prepError) || prepared is null)
                    return Results.Problem(statusCode: 400, title: prepError ?? "invalid_quick_cv_text");

                await storage.PutAsync(objectKey, bytes, "text/plain", ct);
                try
                {
                    var file = new StoredFile
                    {
                        OwnerUserId = uid, ObjectKey = objectKey, Provider = storage.Provider,
                        OriginalName = "quick-match-cv.txt", MimeType = "text/plain",
                        SizeBytes = bytes.LongLength, Sha256 = sha, Status = "ready",
                        CreatedAt = now, VerifiedAt = now
                    };
                    var resume = new Resume
                    {
                        CandidateId = uid, Title = $"CV đối chiếu nhanh ({now:dd/MM/yyyy HH:mm})",
                        RowVersion = 1, CreatedAt = now, UpdatedAt = now
                    };
                    var version = new ResumeVersion
                    {
                        ResumeId = resume.Id, CandidateId = uid, FileId = file.Id,
                        VersionNo = 1, DocumentHash = sha, CreatedAt = now
                    };
                    cvRevision = new ResumeDataRevision
                    {
                        ResumeVersionId = version.Id, CandidateId = uid, RevisionNo = 1,
                        Origin = "user_entered", SchemaVersion = ResumeBuilderRevisionFactory.SchemaVersion,
                        StructuredData = prepared.StructuredData, SourceText = prepared.SourceText,
                        Evidence = prepared.Evidence, DataHash = prepared.DataHash,
                        ParserVersion = prepared.ParserVersion, ConfirmedBy = uid, ConfirmedAt = now, CreatedAt = now
                    };
                    db.StoredFiles.Add(file);
                    db.Resumes.Add(resume);
                    db.ResumeVersions.Add(version);
                    db.ResumeDataRevisions.Add(cvRevision);
                    await db.SaveChangesAsync(ct);
                    resumeVersionId = version.Id;
                }
                catch
                {
                    await storage.DeleteAsync(objectKey, CancellationToken.None);
                    throw;
                }
            }

            try
            {
                if (input.JdSource == "platform")
                {
                    if (input.JobVersionId is not { } jvid || jvid == Guid.Empty)
                        return Results.Problem(statusCode: 400, title: "job_version_required");
                    var pairService = new PairMatchingService(db, jobs, clock);
                    var job = await pairService.Enqueue(uid, new CreatePairRequest(resumeVersionId, jvid, input.IdempotencyKey, "self", null, careerContext), ct);
                    return Results.Accepted($"/api/matching/pairs/{job.Id}", new { job.Id, job.Status });
                }

                var jdTitle = string.IsNullOrWhiteSpace(input.JdTitle) ? "JD đối chiếu nhanh" : input.JdTitle.Trim();
                if (jdTitle.Length > 200) jdTitle = jdTitle[..200];
                var rawJd = input.JdText?.ReplaceLineEndings("\n").Trim() ?? "";
                if (rawJd.Length is < 10 or > 12000 || rawJd.Contains('\0'))
                    return Results.Problem(statusCode: 400, title: "invalid_quick_jd_text");

                if (!JobRevisionFactory.TryExtract(Guid.Empty, null, 1, rawJd, now, out var extractedJd, out var extractError) || extractedJd is null)
                    return Results.Problem(statusCode: 422, title: extractError ?? "quick_jd_extraction_failed");

                var reqs = JsonSerializer.Deserialize<JobStructuredRequirements>(extractedJd.StructuredData, Json)?.Requirements ?? [];
                if (reqs.Count == 0)
                    return Results.Problem(statusCode: 422, title: "no_assessable_requirements");

                var quickJd = new QuickMatchJd
                {
                    CandidateId = uid,
                    SourceType = input.JdSource,
                    SourceUrl = input.JdSource == "url" ? input.JdUrl?.Trim()[..Math.Min(2048, input.JdUrl.Trim().Length)] : null,
                    Title = jdTitle,
                    RawText = rawJd,
                    RequirementsJson = JsonSerializer.Serialize(reqs, Json),
                    CreatedAt = now
                };
                db.QuickMatchJds.Add(quickJd);
                await db.SaveChangesAsync(ct);

                var payload = PairMatchingContract.Serialize(PairMatchingContract.BuildQuick(cvRevision, reqs));
                var matchInput = new MatchInput
                {
                    CandidateId = uid,
                    ResumeVersionId = resumeVersionId,
                    ResumeRevisionId = cvRevision.Id,
                    QuickMatchJdId = quickJd.Id,
                    Context = "self",
                    CareerContext = careerContext,
                    Payload = payload,
                    Fingerprint = PairMatchingContract.Hash($"{resumeVersionId}:{cvRevision.Id}:{cvRevision.DataHash}:quick:{quickJd.Id}:{payload}")
                };
                var aiJob = await jobs.EnqueueMatch(uid, input.IdempotencyKey, matchInput, ct);
                return Results.Accepted($"/api/matching/pairs/{aiJob.Id}", new { aiJob.Id, aiJob.Status });
            }
            catch (JobError e)
            {
                return Results.Problem(statusCode: e.Status, title: e.Code);
            }
        }).RequireAuthorization(p => p.RequireRole("Candidate")).RequireRateLimiting("matching");
    }
}
