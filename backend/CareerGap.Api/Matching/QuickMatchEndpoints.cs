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

            var now = clock.GetUtcNow();

            // 1. Validate CV inputs completely before writing any file or database row.
            Guid? existingResumeVersionId = null;
            ResumeDataRevision? existingCvRevision = null;
            byte[]? pastedCvBytes = null;
            string? pastedCvSha = null;
            PreparedResumeBuilderRevision? preparedCv = null;

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
                existingResumeVersionId = cv.Id;
                existingCvRevision = confirmedCv;
            }
            else
            {
                var rawCv = input.CvText?.ReplaceLineEndings("\n").Trim() ?? "";
                if (rawCv.Length is < 20 or > 20000 || rawCv.Contains('\0'))
                    return Results.Problem(statusCode: 400, title: "invalid_quick_cv_text");

                pastedCvBytes = Encoding.UTF8.GetBytes(rawCv);
                pastedCvSha = Convert.ToHexStringLower(SHA256.HashData(pastedCvBytes));
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
                preparedCv = prepared;
            }

            // 2. Validate JD inputs completely before writing any file or database row.
            Guid? platformJobVersionId = null;
            string? jdTitle = null;
            string? rawJd = null;
            string? sourceUrl = null;
            IReadOnlyList<JobStructuredRequirement>? extractedReqs = null;

            if (input.JdSource == "platform")
            {
                if (input.JobVersionId is not { } jvid || jvid == Guid.Empty)
                    return Results.Problem(statusCode: 400, title: "job_version_required");
                var platformJob = await (from v in db.JobVersions.AsNoTracking()
                    join j in db.Jobs.AsNoTracking() on v.JobId equals j.Id
                    join c in db.Companies.AsNoTracking() on j.CompanyId equals c.Id
                    where v.Id == jvid && c.ArchivedAt == null &&
                        db.Users.Any(u => u.Id == c.OwnerUserId && u.AccountStatus == "active")
                    select new { v.Id, j.CompanyId, c.OwnerUserId, j.CurrentVersionId, j.Status, j.ApplicationDeadline })
                    .SingleOrDefaultAsync(ct);
                if (platformJob is null ||
                    !await BusinessIdentityEndpoints.Verified(db, platformJob.OwnerUserId, platformJob.CompanyId, ct) ||
                    platformJob.CurrentVersionId != jvid || platformJob.Status != "published" ||
                    platformJob.ApplicationDeadline <= now)
                    return Results.NotFound();
                var hasConfirmedJd = await db.JobDataRevisions.AsNoTracking().AnyAsync(x => x.JobVersionId == jvid &&
                    x.Origin == "user_confirmed" && x.ConfirmedBy == platformJob.OwnerUserId && x.ConfirmedAt != null, ct);
                if (!hasConfirmedJd) return Results.Problem(statusCode: 409, title: "confirmed_job_required");
                platformJobVersionId = jvid;
            }
            else
            {
                jdTitle = string.IsNullOrWhiteSpace(input.JdTitle) ? "JD đối chiếu nhanh" : input.JdTitle.Trim();
                if (jdTitle.Length > 200) jdTitle = jdTitle[..200];
                rawJd = input.JdText?.ReplaceLineEndings("\n").Trim() ?? "";
                if (rawJd.Length is < 10 or > 12000 || rawJd.Contains('\0'))
                    return Results.Problem(statusCode: 400, title: "invalid_quick_jd_text");
                if (!JobRevisionFactory.TryExtract(Guid.Empty, null, 1, rawJd, now, out var extractedJd, out var extractError) || extractedJd is null)
                    return Results.Problem(statusCode: 422, title: extractError ?? "quick_jd_extraction_failed");
                extractedReqs = JsonSerializer.Deserialize<JobStructuredRequirements>(extractedJd.StructuredData, Json)?.Requirements ?? [];
                if (extractedReqs.Count == 0)
                    return Results.Problem(statusCode: 422, title: "no_assessable_requirements");
                sourceUrl = input.JdSource == "url" && !string.IsNullOrWhiteSpace(input.JdUrl)
                    ? input.JdUrl.Trim()[..Math.Min(2048, input.JdUrl.Trim().Length)] : null;
            }

            // 3. Enforce idempotency contract (job type + payload parity) before persisting anything.
            var previous = await db.AiJobs.AsNoTracking().SingleOrDefaultAsync(x => x.RequestedBy == uid && x.IdempotencyKey == input.IdempotencyKey, ct);
            if (previous is not null)
            {
                if (previous.JobType != "match_pair")
                    return Results.Problem(statusCode: 409, title: "idempotency_conflict");
                var pinned = await db.MatchInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == previous.Id, ct);
                if (pinned is null || pinned.Context != "self" || pinned.CareerContext != careerContext)
                    return Results.Problem(statusCode: 409, title: "idempotency_conflict");

                bool cvMatches;
                if (input.CvSource == "existing")
                    cvMatches = pinned.ResumeVersionId == existingResumeVersionId;
                else
                {
                    var pinnedHash = await db.ResumeVersions.AsNoTracking().Where(x => x.Id == pinned.ResumeVersionId)
                        .Select(x => x.DocumentHash).SingleOrDefaultAsync(ct);
                    cvMatches = pinnedHash == pastedCvSha;
                }

                bool jdMatches;
                if (input.JdSource == "platform")
                    jdMatches = pinned.QuickMatchJdId is null && pinned.JobVersionId == platformJobVersionId;
                else
                {
                    var pinnedJd = pinned.QuickMatchJdId is { } qid
                        ? await db.QuickMatchJds.AsNoTracking().SingleOrDefaultAsync(x => x.Id == qid && x.CandidateId == uid, ct)
                        : null;
                    jdMatches = pinnedJd is not null && pinnedJd.SourceType == input.JdSource &&
                        pinnedJd.Title == jdTitle && pinnedJd.RawText == rawJd && pinnedJd.SourceUrl == sourceUrl;
                }

                if (!cvMatches || !jdMatches)
                    return Results.Problem(statusCode: 409, title: "idempotency_conflict");
                return Results.Accepted($"/api/matching/pairs/{previous.Id}", new { previous.Id, previous.Status });
            }

            // 4. Persist temporary pasted artifacts only after all validations pass, and compensate on admission failure.
            Guid? createdFileId = null;
            Guid? createdResumeId = null;
            Guid? createdVersionId = null;
            Guid? createdRevisionId = null;
            string? createdObjectKey = null;
            Guid? createdQuickJdId = null;

            async Task CleanupCreatedAsync()
            {
                db.ChangeTracker.Clear();
                if (createdQuickJdId is not null || createdRevisionId is not null || createdVersionId is not null || createdResumeId is not null || createdFileId is not null)
                {
                    await using var cleanupTx = await db.Database.BeginTransactionAsync(CancellationToken.None);
                    await db.Database.ExecuteSqlRawAsync("SET LOCAL careergap.account_purge = 'on'", CancellationToken.None);
                    if (createdQuickJdId is { } qjd)
                        await db.QuickMatchJds.Where(x => x.Id == qjd).ExecuteDeleteAsync(CancellationToken.None);
                    if (createdRevisionId is { } rrid)
                        await db.ResumeDataRevisions.Where(x => x.Id == rrid).ExecuteDeleteAsync(CancellationToken.None);
                    if (createdVersionId is { } rvid)
                        await db.ResumeVersions.Where(x => x.Id == rvid).ExecuteDeleteAsync(CancellationToken.None);
                    if (createdResumeId is { } rid)
                        await db.Resumes.Where(x => x.Id == rid).ExecuteDeleteAsync(CancellationToken.None);
                    if (createdFileId is { } fid)
                        await db.StoredFiles.Where(x => x.Id == fid).ExecuteDeleteAsync(CancellationToken.None);
                    await cleanupTx.CommitAsync(CancellationToken.None);
                }
                if (createdObjectKey is not null)
                    try { await storage.DeleteAsync(createdObjectKey, CancellationToken.None); } catch { }
            }

            try
            {
                Guid resumeVersionId;
                ResumeDataRevision cvRevision;
                if (input.CvSource == "existing")
                {
                    resumeVersionId = existingResumeVersionId!.Value;
                    cvRevision = existingCvRevision!;
                }
                else
                {
                    var objectKey = Guid.NewGuid().ToString("N");
                    await storage.PutAsync(objectKey, pastedCvBytes!, "text/plain", ct);
                    createdObjectKey = objectKey;

                    var file = new StoredFile
                    {
                        OwnerUserId = uid, ObjectKey = objectKey, Provider = storage.Provider,
                        OriginalName = "quick-match-cv.txt", MimeType = "text/plain",
                        SizeBytes = pastedCvBytes!.LongLength, Sha256 = pastedCvSha, Status = "ready",
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
                        VersionNo = 1, DocumentHash = pastedCvSha!, CreatedAt = now
                    };
                    cvRevision = new ResumeDataRevision
                    {
                        ResumeVersionId = version.Id, CandidateId = uid, RevisionNo = 1,
                        Origin = "user_entered", SchemaVersion = ResumeBuilderRevisionFactory.SchemaVersion,
                        StructuredData = preparedCv!.StructuredData, SourceText = preparedCv.SourceText,
                        Evidence = preparedCv.Evidence, DataHash = preparedCv.DataHash,
                        ParserVersion = preparedCv.ParserVersion, ConfirmedBy = uid, ConfirmedAt = now, CreatedAt = now
                    };
                    db.StoredFiles.Add(file);
                    db.Resumes.Add(resume);
                    db.ResumeVersions.Add(version);
                    db.ResumeDataRevisions.Add(cvRevision);
                    await db.SaveChangesAsync(ct);
                    createdFileId = file.Id;
                    createdResumeId = resume.Id;
                    createdVersionId = version.Id;
                    createdRevisionId = cvRevision.Id;
                    resumeVersionId = version.Id;
                }

                if (input.JdSource == "platform")
                {
                    var pairService = new PairMatchingService(db, jobs, clock);
                    var job = await pairService.Enqueue(uid, new CreatePairRequest(resumeVersionId, platformJobVersionId!.Value, input.IdempotencyKey, "self", null, careerContext), ct);
                    return Results.Accepted($"/api/matching/pairs/{job.Id}", new { job.Id, job.Status });
                }

                var quickJd = new QuickMatchJd
                {
                    CandidateId = uid,
                    SourceType = input.JdSource,
                    SourceUrl = sourceUrl,
                    Title = jdTitle!,
                    RawText = rawJd!,
                    RequirementsJson = JsonSerializer.Serialize(extractedReqs!, Json),
                    CreatedAt = now
                };
                db.QuickMatchJds.Add(quickJd);
                await db.SaveChangesAsync(ct);
                createdQuickJdId = quickJd.Id;

                var payload = PairMatchingContract.Serialize(PairMatchingContract.BuildQuick(cvRevision, extractedReqs!));
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
                await CleanupCreatedAsync();
                return Results.Problem(statusCode: e.Status, title: e.Code);
            }
            catch
            {
                await CleanupCreatedAsync();
                throw;
            }
        }).RequireAuthorization(p => p.RequireRole("Candidate")).RequireRateLimiting("matching");
    }
}
