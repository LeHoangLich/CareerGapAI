using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CareerGap.Api.Files;
using CareerGap.Api.Recruitment;

namespace CareerGap.Api.Matching;

public sealed record MatchingEvaluationSkill(string Name, string SourceType);
public sealed record MatchingEvaluationRequirement(string Text, string Importance, string SourceType);
public sealed record MatchingEvaluationConfirmedResumeRevision(string Origin, int SchemaVersion,
    bool ConfirmedByOwner, MatchingEvaluationSkill[] Skills);
public sealed record MatchingEvaluationConfirmedJobRevision(string Origin, int SchemaVersion,
    bool ConfirmedByOwner, MatchingEvaluationRequirement[] Requirements);
public sealed record MatchingEvaluationResume(string Id, string GroupId, string Language, string Text)
{
    public MatchingEvaluationConfirmedResumeRevision? ConfirmedRevision { get; init; }
}
public sealed record MatchingEvaluationJob(string Id, string GroupId, string Language, string Title,
    string Requirements, string Description)
{
    public MatchingEvaluationConfirmedJobRevision? ConfirmedRevision { get; init; }
}
public sealed record MatchingEvaluationLabel(string ResumeGroupId, string JobGroupId, int Relevance);
public sealed class MatchingEvaluationDataset
{
    public string DatasetId { get; set; } = "";
    public string Split { get; set; } = "";
    public MatchingEvaluationResume[] Resumes { get; set; } = [];
    public MatchingEvaluationJob[] Jobs { get; set; } = [];
    public MatchingEvaluationLabel[] Labels { get; set; } = [];
}

public sealed record MatchingEvaluationRankedItem(string QueryGroupId, string ResumeId, string ResumeGroupId,
    string JobId, string JobGroupId, int Rank, double RetrievalScore, int MatchedTermCount, int QueryTermCount);
public sealed record MatchingEvaluationQueryMetric(int QueryCount, int QueryWithLabeledPositives,
    int CompleteLabelQueries, int LabeledPairsInTopK, int PossiblePairsInTopK, int PositivePairsRetrieved,
    int LabeledPositivePairs, double? MeanRecallAtKAmongLabeledPositives, double? PrecisionAtKOnCompleteQueries,
    double? NdcgAtKOnCompleteQueries, double? MeanLabelCoverageAtK, double OverallLabeledPairCoverage);
public sealed record MatchingEvaluationDirection(IReadOnlyList<MatchingEvaluationRankedItem> TopResults,
    MatchingEvaluationQueryMetric Metrics);
public sealed record MatchingRevisionLabelSummary(int Relevance, int LabeledPairCount,
    int CandidateViewEligiblePairs, int CandidateViewPairsWithOverlap, int CandidateViewMatchedRequirements,
    int CandidateViewRequiredRequirements, int CandidateViewMatchedRequiredRequirements,
    int PublicViewEligiblePairs, int PublicViewPairsWithOverlap, int PublicViewMatchedRequirements,
    int PublicViewRequiredRequirements, int PublicViewMatchedRequiredRequirements);
public sealed record MatchingRevisionEvaluation(string MethodVersion, int LabeledPairCount,
    int CandidateViewEligiblePairs, int CandidateViewPairsWithOverlap, int PublicViewEligiblePairs,
    int PublicViewPairsWithOverlap, IReadOnlyDictionary<string, int> ExcludedCandidatePairsByStatus,
    IReadOnlyDictionary<string, int> ExcludedPublicPairsByStatus,
    IReadOnlyList<MatchingRevisionLabelSummary> ByRelevance);
public sealed record MatchingEvaluationResult(Guid RunId, string DatasetId, string Split, DateTimeOffset RanAtUtc,
    string MethodVersion, string ConfigurationHash, string InputSha256, string SourceAssemblySha256,
    int TopK, int ResumeCount, int JobCount,
    int LabeledPairCount, string SourceRevision, MatchingEvaluationDirection CvToJobs,
    MatchingEvaluationDirection JobsToResumes)
{
    public MatchingRevisionEvaluation? ConfirmedRevisionEvaluation { get; init; }
}

public static class MatchingEvaluationRunner
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<MatchingEvaluationResult> RunAsync(string datasetPath, int? requestedTopK,
        KeywordBaselineOptions options, CancellationToken ct)
    {
        KeywordBaselineScorer.Validate(options);
        var topK = requestedTopK ?? options.DefaultTopK;
        if (topK < 1 || topK > options.MaximumTopK) throw new InvalidDataException("invalid_top_k");
        var raw = await File.ReadAllBytesAsync(datasetPath, ct);
        if (raw.Length is 0 or > 10 * 1024 * 1024) throw new InvalidDataException("evaluation_dataset_size");
        MatchingEvaluationDataset dataset;
        try
        {
            dataset = JsonSerializer.Deserialize<MatchingEvaluationDataset>(raw, JsonOptions)
                ?? throw new InvalidDataException("evaluation_dataset_invalid");
        }
        catch (JsonException ex) { throw new InvalidDataException("evaluation_dataset_invalid", ex); }
        ValidateDataset(dataset);
        var labels = dataset.Labels.ToDictionary(x => (x.ResumeGroupId, x.JobGroupId), x => x.Relevance);
        var resumeIndexes = dataset.Resumes.Select((item, index) => (item.GroupId, index))
            .ToDictionary(x => x.GroupId, x => x.index, StringComparer.Ordinal);
        var jobIndexes = dataset.Jobs.Select((item, index) => (item.GroupId, index))
            .ToDictionary(x => x.GroupId, x => x.index, StringComparer.Ordinal);
        var resumeTerms = dataset.Resumes.Select(x => KeywordBaselineScorer.PrepareResumeTerms(x.Text, options)).ToArray();
        var jobWeights = dataset.Jobs.Select(x => KeywordBaselineScorer.PrepareJobWeights(
            new(x.Title, x.Requirements, x.Description), options)).ToArray();
        var pairScores = new KeywordBaselineScore[dataset.Resumes.Length, dataset.Jobs.Length];
        for (var resumeIndex = 0; resumeIndex < dataset.Resumes.Length; resumeIndex++)
        for (var jobIndex = 0; jobIndex < dataset.Jobs.Length; jobIndex++)
        {
            ct.ThrowIfCancellationRequested();
            pairScores[resumeIndex, jobIndex] = KeywordBaselineScorer.ScorePreparedValidated(
                resumeTerms[resumeIndex], jobWeights[jobIndex], options);
        }

        var cvToJobs = BuildDirection(dataset, pairScores, resumeIndexes, jobIndexes, labels, topK, fromResumes: true);
        var jobsToResumes = BuildDirection(dataset, pairScores, resumeIndexes, jobIndexes, labels, topK, fromResumes: false);
        var confirmedRevisionEvaluation = BuildConfirmedRevisionEvaluation(dataset, options);
        return new MatchingEvaluationResult(Guid.NewGuid(), dataset.DatasetId, dataset.Split, DateTimeOffset.UtcNow,
            options.MethodVersion, KeywordBaselineScorer.ConfigurationHash(options), Convert.ToHexStringLower(SHA256.HashData(raw)),
            ExecutingAssemblySha256(),
            topK, dataset.Resumes.Length, dataset.Jobs.Length, labels.Count, "working-tree", cvToJobs, jobsToResumes)
        {
            ConfirmedRevisionEvaluation = confirmedRevisionEvaluation
        };
    }

    private static MatchingRevisionEvaluation? BuildConfirmedRevisionEvaluation(MatchingEvaluationDataset dataset,
        KeywordBaselineOptions options)
    {
        if (!dataset.Resumes.Any(x => x.ConfirmedRevision is not null) &&
            !dataset.Jobs.Any(x => x.ConfirmedRevision is not null)) return null;
        var resumes = dataset.Resumes.ToDictionary(x => x.GroupId, StringComparer.Ordinal);
        var jobs = dataset.Jobs.ToDictionary(x => x.GroupId, StringComparer.Ordinal);
        var excludedCandidate = new Dictionary<string, int>(StringComparer.Ordinal);
        var excludedPublic = new Dictionary<string, int>(StringComparer.Ordinal);
        var mutable = Enumerable.Range(0, 4).Select(_ => new MutableRevisionLabelSummary()).ToArray();
        var candidateEligible = 0;
        var candidateOverlap = 0;
        var publicEligible = 0;
        var publicOverlap = 0;
        foreach (var label in dataset.Labels)
        {
            var resume = resumes[label.ResumeGroupId];
            var job = jobs[label.JobGroupId];
            var row = mutable[label.Relevance];
            row.LabeledPairCount++;
            var resumeRevision = resume.ConfirmedRevision;
            var jobRevision = job.ConfirmedRevision;
            var jobRequirements = ValidRequirements(jobRevision);
            var candidateSkills = ValidSkills(resumeRevision, includeStatements: true);
            var publicSkills = ValidSkills(resumeRevision, includeStatements: false);
            var candidateStatus = EligibilityStatus(resumeRevision, jobRevision, candidateSkills, jobRequirements);
            var publicStatus = EligibilityStatus(resumeRevision, jobRevision, publicSkills, jobRequirements,
                publicScope: true);
            if (candidateStatus is not null)
                Increment(excludedCandidate, candidateStatus);
            else
            {
                candidateEligible++;
                row.CandidateViewEligiblePairs++;
                var counts = CountRequirementOverlaps(candidateSkills!, jobRequirements!, options);
                row.CandidateViewMatchedRequirements += counts.Matched;
                row.CandidateViewRequiredRequirements += counts.Required;
                row.CandidateViewMatchedRequiredRequirements += counts.MatchedRequired;
                if (counts.Matched > 0) { candidateOverlap++; row.CandidateViewPairsWithOverlap++; }
            }
            if (publicStatus is not null)
                Increment(excludedPublic, publicStatus);
            else
            {
                publicEligible++;
                row.PublicViewEligiblePairs++;
                var counts = CountRequirementOverlaps(publicSkills!, jobRequirements!, options);
                row.PublicViewMatchedRequirements += counts.Matched;
                row.PublicViewRequiredRequirements += counts.Required;
                row.PublicViewMatchedRequiredRequirements += counts.MatchedRequired;
                if (counts.Matched > 0) { publicOverlap++; row.PublicViewPairsWithOverlap++; }
            }
        }
        return new(ConfirmedRevisionMatcher.MethodVersion, dataset.Labels.Length,
            candidateEligible, candidateOverlap, publicEligible, publicOverlap,
            excludedCandidate, excludedPublic, mutable.Select((x, index) => x.ToRecord(index)).ToArray());
    }

    private static MatchingEvaluationSkill[]? ValidSkills(MatchingEvaluationConfirmedResumeRevision? revision,
        bool includeStatements)
    {
        if (revision is null || revision.Origin != "user_confirmed" || !revision.ConfirmedByOwner ||
            revision.SchemaVersion != ResumeRevisionFactory.SchemaVersion || revision.Skills is null ||
            revision.Skills.Length > 20 || revision.Skills.Any(x => x is null || string.IsNullOrWhiteSpace(x.Name) ||
                x.Name.Length > 100 || x.SourceType is not ("document_span" or "user_statement")) ||
            revision.Skills.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != revision.Skills.Length)
            return null;
        return revision.Skills.Where(x => includeStatements || x.SourceType == "document_span").ToArray();
    }

    private static MatchingEvaluationRequirement[]? ValidRequirements(MatchingEvaluationConfirmedJobRevision? revision)
    {
        if (revision is null || revision.Origin != "user_confirmed" || !revision.ConfirmedByOwner ||
            revision.SchemaVersion is not (1 or JobRevisionFactory.SchemaVersion) || revision.Requirements is null ||
            revision.Requirements.Length > JobRevisionFactory.MaxRequirements ||
            revision.Requirements.Any(x => x is null || string.IsNullOrWhiteSpace(x.Text) || x.Text.Length > 2000 ||
                x.Importance is not ("required" or "optional" or "unspecified") ||
                x.SourceType is not ("document_text" or "user_statement"))) return null;
        return revision.Requirements;
    }

    private static string? EligibilityStatus(MatchingEvaluationConfirmedResumeRevision? resumeRevision,
        MatchingEvaluationConfirmedJobRevision? jobRevision, MatchingEvaluationSkill[]? skills,
        MatchingEvaluationRequirement[]? requirements, bool publicScope = false)
    {
        if (resumeRevision is null || resumeRevision.Origin != "user_confirmed" || !resumeRevision.ConfirmedByOwner)
            return "resume_revision_unconfirmed";
        if (jobRevision is null || jobRevision.Origin != "user_confirmed" || !jobRevision.ConfirmedByOwner)
            return "job_revision_unconfirmed";
        if (skills is null) return "resume_revision_data_unavailable";
        if (requirements is null) return "job_revision_data_unavailable";
        if (skills.Length == 0) return publicScope ? "no_public_document_skills" : "resume_revision_empty";
        if (requirements.Length == 0) return "job_revision_empty";
        return null;
    }

    private static (int Matched, int Required, int MatchedRequired) CountRequirementOverlaps(
        IReadOnlyList<MatchingEvaluationSkill> skills, IReadOnlyList<MatchingEvaluationRequirement> requirements,
        KeywordBaselineOptions options)
    {
        var mapped = requirements.Select((x, index) => new JobStructuredRequirement(
            $"evaluation-{index + 1}", x.Text, x.Importance, x.SourceType)).ToArray();
        var result = ConfirmedRevisionMatcher.CountRequirementOverlaps(skills.Select(x => x.Name).ToArray(), mapped, options);
        return (result.Matched, result.Required, result.MatchedRequired);
    }

    private static void Increment(IDictionary<string, int> target, string key) =>
        target[key] = target.TryGetValue(key, out var count) ? count + 1 : 1;

    private sealed class MutableRevisionLabelSummary
    {
        public int LabeledPairCount;
        public int CandidateViewEligiblePairs;
        public int CandidateViewPairsWithOverlap;
        public int CandidateViewMatchedRequirements;
        public int CandidateViewRequiredRequirements;
        public int CandidateViewMatchedRequiredRequirements;
        public int PublicViewEligiblePairs;
        public int PublicViewPairsWithOverlap;
        public int PublicViewMatchedRequirements;
        public int PublicViewRequiredRequirements;
        public int PublicViewMatchedRequiredRequirements;
        public MatchingRevisionLabelSummary ToRecord(int relevance) => new(relevance, LabeledPairCount,
            CandidateViewEligiblePairs, CandidateViewPairsWithOverlap, CandidateViewMatchedRequirements,
            CandidateViewRequiredRequirements, CandidateViewMatchedRequiredRequirements,
            PublicViewEligiblePairs, PublicViewPairsWithOverlap, PublicViewMatchedRequirements,
            PublicViewRequiredRequirements, PublicViewMatchedRequiredRequirements);
    }

    private static string ExecutingAssemblySha256()
    {
        try
        {
            var path = typeof(MatchingEvaluationRunner).Assembly.Location;
            return string.IsNullOrEmpty(path) || !File.Exists(path) ? "unknown" :
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        }
        catch (IOException) { return "unknown"; }
    }

    public static async Task<string> ReadGitRevisionAsync()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null) return "unknown";
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return process.ExitCode == 0 ? output.Trim() : "unknown";
        }
        catch { return "unknown"; }
    }

    private static MatchingEvaluationDirection BuildDirection(MatchingEvaluationDataset dataset,
        KeywordBaselineScore[,] scores,
        IReadOnlyDictionary<string, int> resumeIndexes,
        IReadOnlyDictionary<string, int> jobIndexes,
        IReadOnlyDictionary<(string ResumeGroupId, string JobGroupId), int> labels, int topK, bool fromResumes)
    {
        var rows = new List<MatchingEvaluationRankedItem>();
        var recallValues = new List<double>();
        var precisionValues = new List<double>();
        var ndcgValues = new List<double>();
        var coverageValues = new List<double>();
        var queriesWithPositives = 0;
        var completeQueries = 0;
        var labeledInTop = 0;
        var possibleInTop = 0;
        var positiveRetrieved = 0;
        var labeledPositivePairs = 0;
        var targetCount = fromResumes ? dataset.Jobs.Length : dataset.Resumes.Length;
        var totalPairs = (long)dataset.Resumes.Length * dataset.Jobs.Length;

        var queryGroups = fromResumes
            ? dataset.Resumes.Select(x => (x.GroupId, x.Id)).ToArray()
            : dataset.Jobs.Select(x => (x.GroupId, x.Id)).ToArray();
        foreach (var query in queryGroups)
        {
            var queryIndex = fromResumes ? resumeIndexes[query.GroupId] : jobIndexes[query.GroupId];
            var ranking = fromResumes
                ? dataset.Jobs.Select((job, index) => (CandidateId: job.Id, CandidateGroup: job.GroupId, Score: scores[queryIndex, index]))
                : dataset.Resumes.Select((resume, index) => (CandidateId: resume.Id, CandidateGroup: resume.GroupId, Score: scores[index, queryIndex]));
            var ordered = ranking.Where(x => x.Score.RetrievalScore is not null && x.Score.MatchedTermCount > 0)
                .OrderByDescending(x => x.Score.RetrievalScore).ThenBy(x => x.CandidateId, StringComparer.Ordinal).ToArray();
            var top = ordered.Take(topK).ToArray();
            for (var i = 0; i < top.Length; i++)
            {
                var resume = fromResumes ? dataset.Resumes[queryIndex] : dataset.Resumes[resumeIndexes[top[i].CandidateGroup]];
                var job = fromResumes ? dataset.Jobs[jobIndexes[top[i].CandidateGroup]] : dataset.Jobs[queryIndex];
                rows.Add(new(query.GroupId, resume.Id, resume.GroupId, job.Id, job.GroupId, i + 1,
                    top[i].Score.RetrievalScore!.Value, top[i].Score.MatchedTermCount, top[i].Score.QueryTermCount));
            }

            var queryPairs = fromResumes
                ? dataset.Jobs.Select(job => (ResumeGroupId: query.GroupId, JobGroupId: job.GroupId))
                : dataset.Resumes.Select(resume => (ResumeGroupId: resume.GroupId, JobGroupId: query.GroupId));
            var allPairs = queryPairs.ToArray();
            var known = allPairs.Where(labels.ContainsKey).ToHashSet();
            var positives = known.Where(pair => labels[pair] > 0).ToHashSet();
            var retrievedKnown = top.Select(x => fromResumes
                    ? (ResumeGroupId: query.GroupId, JobGroupId: x.CandidateGroup)
                    : (ResumeGroupId: x.CandidateGroup, JobGroupId: query.GroupId))
                .Where(known.Contains).ToArray();
            var hits = retrievedKnown.Count(positives.Contains);
            labeledInTop += retrievedKnown.Length;
            possibleInTop += Math.Min(topK, targetCount);
            positiveRetrieved += hits;
            labeledPositivePairs += positives.Count;
            var coverage = Math.Min(topK, targetCount) == 0 ? 0d : (double)retrievedKnown.Length / Math.Min(topK, targetCount);
            coverageValues.Add(coverage);
            if (positives.Count > 0)
            {
                queriesWithPositives++;
                recallValues.Add((double)hits / positives.Count);
            }
            if (known.Count == allPairs.Length)
            {
                completeQueries++;
                var denominator = Math.Min(topK, targetCount);
                if (denominator > 0)
                {
                    precisionValues.Add((double)hits / denominator);
                    var dcg = 0d;
                    for (var i = 0; i < top.Length; i++)
                    {
                        var pair = fromResumes
                            ? (ResumeGroupId: query.GroupId, JobGroupId: top[i].CandidateGroup)
                            : (ResumeGroupId: top[i].CandidateGroup, JobGroupId: query.GroupId);
                        if (labels.TryGetValue(pair, out var relevance) && relevance > 0)
                            dcg += (Math.Pow(2, relevance) - 1) / Math.Log2(i + 2);
                    }
                    var ideal = positives.Select(pair => labels[pair]).OrderDescending().Take(topK).ToArray();
                    var idcg = ideal.Select((relevance, index) => (Math.Pow(2, relevance) - 1) / Math.Log2(index + 2)).Sum();
                    ndcgValues.Add(idcg == 0 ? 0 : dcg / idcg);
                }
            }
        }

        return new(rows, new(queryGroups.Length, queriesWithPositives, completeQueries, labeledInTop,
            possibleInTop, positiveRetrieved, labeledPositivePairs, Average(recallValues), Average(precisionValues),
            Average(ndcgValues), Average(coverageValues), totalPairs == 0 ? 0 : (double)labels.Count / totalPairs));
    }

    private static double? Average(IReadOnlyCollection<double> values) => values.Count == 0 ? null : values.Average();

    private static void ValidateDataset(MatchingEvaluationDataset dataset)
    {
        if (dataset.Resumes is null || dataset.Jobs is null || dataset.Labels is null ||
            string.IsNullOrWhiteSpace(dataset.DatasetId) || dataset.DatasetId.Length > 120 ||
            string.IsNullOrWhiteSpace(dataset.Split) || dataset.Split.Length > 40 ||
            dataset.Resumes.Length is < 1 or > 1000 || dataset.Jobs.Length is < 1 or > 1000 || dataset.Labels.Length > 1_000_000)
            throw new InvalidDataException("evaluation_dataset_invalid");
        if (dataset.Resumes.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 120 ||
                string.IsNullOrWhiteSpace(x.GroupId) || x.GroupId.Length > 120 || string.IsNullOrWhiteSpace(x.Text) ||
                x.Text.Length > 100_000 || x.Language is null || x.Language.Length > 20) ||
            dataset.Jobs.Any(x => x is null || string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 120 ||
                string.IsNullOrWhiteSpace(x.GroupId) || x.GroupId.Length > 120 || string.IsNullOrWhiteSpace(x.Title) ||
                x.Title.Length > 10_000 || x.Requirements is null || x.Requirements.Length > 100_000 ||
                x.Description is null || x.Description.Length > 100_000 || x.Language is null || x.Language.Length > 20) ||
            dataset.Labels.Any(x => x is null || string.IsNullOrWhiteSpace(x.ResumeGroupId) ||
                string.IsNullOrWhiteSpace(x.JobGroupId)))
            throw new InvalidDataException("evaluation_document_invalid");
        if (dataset.Resumes.Any(x => x.ConfirmedRevision is { } revision &&
                (revision.Skills is null || revision.Skills.Length > 20)) ||
            dataset.Jobs.Any(x => x.ConfirmedRevision is { } revision &&
                (revision.Requirements is null || revision.Requirements.Length > JobRevisionFactory.MaxRequirements)))
            throw new InvalidDataException("evaluation_revision_data_invalid");
        if (dataset.Resumes.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != dataset.Resumes.Length ||
            dataset.Resumes.Select(x => x.GroupId).Distinct(StringComparer.Ordinal).Count() != dataset.Resumes.Length ||
            dataset.Jobs.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != dataset.Jobs.Length ||
            dataset.Jobs.Select(x => x.GroupId).Distinct(StringComparer.Ordinal).Count() != dataset.Jobs.Length)
            throw new InvalidDataException("evaluation_groups_must_be_unique_per_split");
        var resumeGroups = dataset.Resumes.Select(x => x.GroupId).ToHashSet(StringComparer.Ordinal);
        var jobGroups = dataset.Jobs.Select(x => x.GroupId).ToHashSet(StringComparer.Ordinal);
        if (dataset.Labels.Any(x => !resumeGroups.Contains(x.ResumeGroupId) || !jobGroups.Contains(x.JobGroupId) || x.Relevance is < 0 or > 3) ||
            dataset.Labels.Select(x => (x.ResumeGroupId, x.JobGroupId)).Distinct().Count() != dataset.Labels.Length)
            throw new InvalidDataException("evaluation_labels_invalid");
        // A split contains one document per group. Keep every CV/JD variant from a person/job
        // in one split so near-duplicates cannot leak across evaluation boundaries.
    }
}
