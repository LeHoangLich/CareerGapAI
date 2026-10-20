using System.Security.Claims;
using System.Security.Cryptography;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareerGap.Api.Matching;

public sealed class KeywordMatchingError(string code, int statusCode) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public sealed record KeywordMatchRun<T>(string MethodVersion, string ConfigurationHash, string ScoreKind,
    DateTimeOffset GeneratedAtUtc, int SearchSpaceCount, int AssessedCount, int UnreadableCount,
    int ZeroOverlapCount, IReadOnlyList<T> Items, int NoQueryTermsCount = 0);
public sealed record KeywordMatchedTerms(int Count, int QueryTermCount, IReadOnlyList<string> Terms);
public sealed record CandidateJobRecommendation(Guid JobId, Guid JobVersionId, string Title, string CompanyName,
    string? Location, string WorkMode, string EmploymentType, DateTimeOffset? ApplicationDeadline,
    double RetrievalScore, KeywordMatchedTerms KeywordOverlap, ConfirmedRevisionDiagnostics ConfirmedRevisionMatch);
public sealed record RecruiterResumeRecommendation(Guid CandidateId, Guid ResumeVersionId, int VersionNo,
    string DisplayName, string? Headline, DateTimeOffset PublishedAt, double RetrievalScore, KeywordMatchedTerms KeywordOverlap,
    ConfirmedRevisionDiagnostics ConfirmedRevisionMatch);

public sealed class KeywordMatchingService(AppDb db, IFileStorage storage, DocumentParser parser,
    TimeProvider clock, IOptions<KeywordBaselineOptions> configured)
{
    private readonly KeywordBaselineOptions options = configured.Value;

    public async Task<KeywordMatchRun<CandidateJobRecommendation>> RecommendJobsAsync(Guid candidateId,
        Guid resumeVersionId, int? requestedTopK, CancellationToken ct)
    {
        ValidateTopK(requestedTopK);
        var source = await (from version in db.ResumeVersions.AsNoTracking()
            join file in db.StoredFiles.AsNoTracking() on new { version.FileId, version.CandidateId }
                equals new { FileId = file.Id, CandidateId = file.OwnerUserId }
            where version.Id == resumeVersionId && version.CandidateId == candidateId
            select new { Version = version, File = file }).SingleOrDefaultAsync(ct);
        if (source is null) throw new KeywordMatchingError("resume_not_found", 404);
        var resumeText = await ReadAndParseAsync(source.File, source.Version.DocumentHash, ct);
        var now = clock.GetUtcNow();
        var jobs = await db.Jobs.AsNoTracking().Include(x => x.Company).Include(x => x.CurrentVersion)
            .Where(x => x.Status == "published" && x.CurrentVersionId != null && x.Company!.ArchivedAt == null &&
                (x.ApplicationDeadline == null || x.ApplicationDeadline > now) &&
                db.Users.Any(owner => owner.Id == x.Company.OwnerUserId && owner.AccountStatus == "active"))
            .ToListAsync(ct);

        var resumeRevision = await db.ResumeDataRevisions.AsNoTracking()
            .Where(x => x.ResumeVersionId == resumeVersionId && x.CandidateId == candidateId &&
                (x.Origin == "user_confirmed" || (x.Origin == "user_entered" && x.SchemaVersion == 2)) && x.ConfirmedBy == candidateId && x.ConfirmedAt != null)
            .OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
        var jobRevisionMap = await LoadJobRevisionsAsync(jobs
            .Where(x => x.CurrentVersion is not null)
            .Select(x => (x.CurrentVersion!.Id, x.Company!.OwnerUserId)), ct);

        var ranked = new List<CandidateJobRecommendation>();
        var noQueryTerms = 0;
        foreach (var job in jobs)
        {
            ct.ThrowIfCancellationRequested();
            if (job.CurrentVersion is null) { noQueryTerms++; continue; }
            var result = KeywordBaselineScorer.Score(resumeText, ToKeywordText(job.CurrentVersion), options);
            if (result.RetrievalScore is null) { noQueryTerms++; continue; }
            if (result.MatchedTermCount == 0) continue;
            jobRevisionMap.TryGetValue(job.CurrentVersion.Id, out var jobRevision);
            var revisionMatch = ConfirmedRevisionMatcher.Evaluate(resumeRevision, jobRevision, options,
                publicResumeScope: false);
            ranked.Add(new(job.Id, job.CurrentVersion.Id, job.CurrentVersion.Title, job.Company!.Name,
                job.CurrentVersion.Location ?? job.Company.Location, job.CurrentVersion.WorkMode,
                job.CurrentVersion.EmploymentType, job.ApplicationDeadline, result.RetrievalScore.Value,
                new(result.MatchedTermCount, result.QueryTermCount, result.MatchedTerms), revisionMatch));
        }

        var ordered = ranked.OrderByDescending(x => x.RetrievalScore).ThenBy(x => x.JobId)
            .Take(requestedTopK ?? options.DefaultTopK).ToArray();
        return new(options.MethodVersion, KeywordBaselineScorer.ConfigurationHash(options), "retrieval_only",
            now, jobs.Count, jobs.Count - noQueryTerms, 0, jobs.Count - noQueryTerms - ranked.Count, ordered, noQueryTerms);
    }

    public async Task<KeywordMatchRun<RecruiterResumeRecommendation>> SearchPublicResumesAsync(Guid recruiterId,
        Guid jobId, int? requestedTopK, CancellationToken ct)
    {
        ValidateTopK(requestedTopK);
        var now = clock.GetUtcNow();
        var job = await db.Jobs.AsNoTracking().Include(x => x.Company).Include(x => x.CurrentVersion)
            .SingleOrDefaultAsync(x => x.Id == jobId && x.Company!.OwnerUserId == recruiterId &&
                x.Status == "published" && x.CurrentVersionId != null && x.Company.ArchivedAt == null &&
                (x.ApplicationDeadline == null || x.ApplicationDeadline > now), ct);
        if (job?.CurrentVersion is null) throw new KeywordMatchingError("job_not_available", 404);

        var jobRevision = await db.JobDataRevisions.AsNoTracking()
            .Where(x => x.JobVersionId == job.CurrentVersion.Id && x.Origin == "user_confirmed" &&
                x.ConfirmedBy == recruiterId && x.ConfirmedAt != null)
            .OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);

        var publicSources = await (from selection in db.CandidatePublicResumes.AsNoTracking()
            join version in db.ResumeVersions.AsNoTracking() on new { selection.ResumeVersionId, selection.CandidateId }
                equals new { ResumeVersionId = version.Id, version.CandidateId }
            join file in db.StoredFiles.AsNoTracking() on new { version.FileId, version.CandidateId }
                equals new { FileId = file.Id, CandidateId = file.OwnerUserId }
            join profile in db.CandidateProfiles.AsNoTracking() on selection.CandidateId equals profile.UserId
            where file.Status == "ready" && file.Sha256 == selection.SourceHash && version.DocumentHash == selection.SourceHash &&
                db.Users.Any(account => account.Id == selection.CandidateId && account.AccountStatus == "active")
            orderby selection.CandidateId
            select new
            {
                Selection = selection,
                Version = version,
                File = file,
                DisplayName = db.CandidateProfileVisibility.Any(v => v.CandidateId == profile.UserId && v.FieldKey == "display_name")
                    ? profile.DisplayName : "Ứng viên",
                Headline = db.CandidateProfileVisibility.Any(v => v.CandidateId == profile.UserId && v.FieldKey == "headline")
                    ? profile.Headline : null
            })
            .ToListAsync(ct);

        var publicResumeIds = publicSources.Select(x => x.Version.Id).Distinct().ToArray();
        var resumeRevisionRows = publicResumeIds.Length == 0
            ? []
            : await db.ResumeDataRevisions.AsNoTracking()
                .Where(x => publicResumeIds.Contains(x.ResumeVersionId) && (x.Origin == "user_confirmed" || (x.Origin == "user_entered" && x.SchemaVersion == 2)) &&
                    x.ConfirmedBy == x.CandidateId && x.ConfirmedAt != null)
                .OrderByDescending(x => x.RevisionNo).ToListAsync(ct);
        var resumeRevisionMap = resumeRevisionRows.GroupBy(x => x.ResumeVersionId)
            .ToDictionary(x => x.Key, x => x.First());

        var jobText = ToKeywordText(job.CurrentVersion);
        var jobWeights = KeywordBaselineScorer.PrepareJobWeights(jobText, options);
        if (jobWeights.Count == 0)
            return new(options.MethodVersion, KeywordBaselineScorer.ConfigurationHash(options), "retrieval_only",
                now, publicSources.Count, 0, 0, 0, [], publicSources.Count);

        var ranked = new List<RecruiterResumeRecommendation>();
        var assessed = 0;
        var unreadable = 0;
        var zeroOverlap = 0;
        var noQueryTerms = 0;
        foreach (var source in publicSources)
        {
            ct.ThrowIfCancellationRequested();
            if (source.File.Provider != storage.Provider)
            {
                unreadable++;
                continue;
            }
            ParsedDocument parsed;
            try
            {
                var bytes = await storage.ReadAsync(source.File.ObjectKey, ct);
                var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
                if (hash != source.Selection.SourceHash || hash != source.File.Sha256)
                {
                    unreadable++;
                    continue;
                }
                parsed = parser.Parse(bytes, source.File.OriginalName, ct);
            }
            catch (DocumentError) { unreadable++; continue; }
            catch (Exception ex) when (ex is IOException or Amazon.S3.AmazonS3Exception or InvalidDataException)
            { unreadable++; continue; }

            var resumeTerms = KeywordBaselineScorer.PrepareResumeTerms(
                string.Join('\n', parsed.Segments.Select(x => x.Text)), options);
            var result = KeywordBaselineScorer.ScorePreparedValidated(resumeTerms, jobWeights, options);
            if (result.RetrievalScore is null) { noQueryTerms++; continue; }
            assessed++;
            if (result.MatchedTermCount == 0) { zeroOverlap++; continue; }
            resumeRevisionMap.TryGetValue(source.Version.Id, out var resumeRevision);
            var revisionMatch = ConfirmedRevisionMatcher.Evaluate(resumeRevision, jobRevision, options,
                publicResumeScope: true);
            ranked.Add(new(source.Selection.CandidateId, source.Version.Id, source.Version.VersionNo,
                source.DisplayName, source.Headline, source.Selection.PublishedAt,
                result.RetrievalScore.Value, new(result.MatchedTermCount, result.QueryTermCount, result.MatchedTerms),
                revisionMatch));
        }

        // A public selection can be hidden or replaced while files are being read. Revalidate the exact
        // candidate/version/hash set before releasing aggregate revision-match diagnostics.
        var publicCandidateIds = publicSources.Select(x => x.Selection.CandidateId).Distinct().ToArray();
        var currentlyPublic = publicCandidateIds.Length == 0
            ? []
            : await db.CandidatePublicResumes.AsNoTracking()
                .Where(x => publicCandidateIds.Contains(x.CandidateId) && x.Status == "ready")
                .Select(x => new { x.CandidateId, x.ResumeVersionId, x.SourceHash })
                .ToListAsync(ct);
        var currentKeys = currentlyPublic.Select(x => (x.CandidateId, x.ResumeVersionId, x.SourceHash)).ToHashSet();
        var originalHashes = publicSources.ToDictionary(x => (x.Selection.CandidateId, x.Version.Id),
            x => x.Selection.SourceHash);
        ranked = ranked.Where(item => originalHashes.TryGetValue((item.CandidateId, item.ResumeVersionId), out var sourceHash) &&
            currentKeys.Contains((item.CandidateId, item.ResumeVersionId, sourceHash))).ToList();

        var ordered = ranked.OrderByDescending(x => x.RetrievalScore).ThenBy(x => x.CandidateId)
            .ThenBy(x => x.ResumeVersionId).Take(requestedTopK ?? options.DefaultTopK).ToArray();
        return new(options.MethodVersion, KeywordBaselineScorer.ConfigurationHash(options), "retrieval_only",
            now, publicSources.Count, assessed, unreadable, zeroOverlap, ordered, noQueryTerms);
    }

    private async Task<string> ReadAndParseAsync(StoredFile file, string expectedHash, CancellationToken ct)
    {
        if (file.Status != "ready" || file.Provider != storage.Provider || file.Sha256 != expectedHash)
            throw new KeywordMatchingError("resume_unavailable", 503);
        byte[] bytes;
        try { bytes = await storage.ReadAsync(file.ObjectKey, ct); }
        catch (Exception ex) when (ex is IOException or Amazon.S3.AmazonS3Exception or InvalidDataException)
        { throw new KeywordMatchingError("resume_unavailable", 503); }
        var actualHash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (actualHash != expectedHash) throw new KeywordMatchingError("resume_unavailable", 503);
        try { return string.Join('\n', parser.Parse(bytes, file.OriginalName, ct).Segments.Select(x => x.Text)); }
        catch (DocumentError ex) { throw new KeywordMatchingError(ex.Code, 422); }
    }

    private void ValidateTopK(int? topK)
    {
        KeywordBaselineScorer.Validate(options);
        if (topK is < 1 || topK > options.MaximumTopK) throw new KeywordMatchingError("invalid_top_k", 400);
    }

    private static JobKeywordText ToKeywordText(JobVersion version) =>
        new(version.Title, version.RequirementsText, version.Description);

    private async Task<Dictionary<Guid, JobDataRevision>> LoadJobRevisionsAsync(
        IEnumerable<(Guid VersionId, Guid OwnerId)> ownedVersions, CancellationToken ct)
    {
        var ownersByVersion = ownedVersions.GroupBy(x => x.VersionId)
            .ToDictionary(x => x.Key, x => x.First().OwnerId);
        if (ownersByVersion.Count == 0) return [];
        var versionIds = ownersByVersion.Keys.ToArray();
        var rows = await db.JobDataRevisions.AsNoTracking()
            .Where(x => versionIds.Contains(x.JobVersionId) && x.Origin == "user_confirmed" && x.ConfirmedAt != null)
            .OrderByDescending(x => x.RevisionNo).ToListAsync(ct);
        return rows.Where(x => ownersByVersion.TryGetValue(x.JobVersionId, out var ownerId) && x.ConfirmedBy == ownerId)
            .GroupBy(x => x.JobVersionId).ToDictionary(x => x.Key, x => x.First());
    }
}

public static class KeywordMatchingEndpoints
{
    public static void MapKeywordMatchingEndpoints(this WebApplication app)
    {
        app.MapGet("/api/matching/jobs", async (Guid resumeVersionId, int? topK, ClaimsPrincipal user,
            KeywordMatchingService matching, CancellationToken ct) =>
        {
            if (!TryUserId(user, out var candidateId)) return Results.Unauthorized();
            try { return Results.Ok(await matching.RecommendJobsAsync(candidateId, resumeVersionId, topK, ct)); }
            catch (KeywordMatchingError ex) { return Results.Problem(statusCode: ex.StatusCode, title: ex.Code); }
        }).RequireAuthorization(policy => policy.RequireRole("Candidate")).RequireRateLimiting("matching");

        app.MapGet("/api/matching/public-resumes", async (Guid jobId, int? topK, ClaimsPrincipal user,
            KeywordMatchingService matching, CancellationToken ct) =>
        {
            if (!TryUserId(user, out var recruiterId)) return Results.Unauthorized();
            try { return Results.Ok(await matching.SearchPublicResumesAsync(recruiterId, jobId, topK, ct)); }
            catch (KeywordMatchingError ex) { return Results.Problem(statusCode: ex.StatusCode, title: ex.Code); }
        }).RequireAuthorization(policy => policy.RequireRole("Recruiter")).RequireRateLimiting("matching");
    }

    private static bool TryUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
