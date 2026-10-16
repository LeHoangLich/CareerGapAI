using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CareerGap.Api.Matching;

public sealed class KeywordBaselineOptions
{
    public string MethodVersion { get; set; } = "keyword-overlap-v1";
    public int DefaultTopK { get; set; } = 20;
    public int MaximumTopK { get; set; } = 50;
    public int MaximumQueryTerms { get; set; } = 500;
    public int ExplanationTermCount { get; set; } = 8;
    public int MinimumTokenLength { get; set; } = 2;
    public decimal TitleWeight { get; set; } = 3;
    public decimal RequirementsWeight { get; set; } = 2;
    public decimal DescriptionWeight { get; set; } = 1;
    public string[] StopWords { get; set; } = [];
}

public sealed record JobKeywordText(string Title, string Requirements, string Description);
public sealed record KeywordBaselineScore(double? RetrievalScore, int MatchedTermCount, int QueryTermCount,
    IReadOnlyList<string> MatchedTerms);

public static class KeywordBaselineScorer
{
    public const string TokenizerVersion = "unicode-letters-digits-accent-fold-tech-tokens-v1";
    private static readonly Regex Tokens = new(
        @"(?<![\p{L}\p{N}])(?:\.net|c\+\+|c#|node\.js|ci/cd|asp\.net|[\p{L}\p{N}][\p{L}\p{N}.+#/-]{0,38})(?![\p{L}\p{N}])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static KeywordBaselineScore Score(string resumeText, JobKeywordText job, KeywordBaselineOptions options)
    {
        Validate(options);
        var stopWords = options.StopWords.Select(NormalizeToken).ToHashSet(StringComparer.Ordinal);
        var resumeTerms = ExtractTerms(resumeText, stopWords, options.MinimumTokenLength, options.MaximumQueryTerms)
            .ToHashSet(StringComparer.Ordinal);
        var queryWeights = BuildJobWeights(job, options, stopWords);
        return ScorePreparedCore(resumeTerms, queryWeights, options);
    }

    public static HashSet<string> PrepareResumeTerms(string resumeText, KeywordBaselineOptions options)
    {
        Validate(options);
        var stopWords = options.StopWords.Select(NormalizeToken).ToHashSet(StringComparer.Ordinal);
        return ExtractTerms(resumeText, stopWords, options.MinimumTokenLength, options.MaximumQueryTerms)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static Dictionary<string, decimal> PrepareJobWeights(JobKeywordText job, KeywordBaselineOptions options)
    {
        Validate(options);
        var stopWords = options.StopWords.Select(NormalizeToken).ToHashSet(StringComparer.Ordinal);
        return BuildJobWeights(job, options, stopWords);
    }

    private static Dictionary<string, decimal> BuildJobWeights(JobKeywordText job,
        KeywordBaselineOptions options, HashSet<string> stopWords)
    {
        var queryWeights = new Dictionary<string, decimal>(StringComparer.Ordinal);
        AddTerms(queryWeights, job.Title, options.TitleWeight, stopWords, options);
        AddTerms(queryWeights, job.Requirements, options.RequirementsWeight, stopWords, options);
        AddTerms(queryWeights, job.Description, options.DescriptionWeight, stopWords, options);
        return queryWeights;
    }

    public static KeywordBaselineScore ScorePrepared(IReadOnlySet<string> resumeTerms,
        IReadOnlyDictionary<string, decimal> queryWeights, KeywordBaselineOptions options)
    {
        Validate(options);
        return ScorePreparedCore(resumeTerms, queryWeights, options);
    }

    internal static KeywordBaselineScore ScorePreparedValidated(IReadOnlySet<string> resumeTerms,
        IReadOnlyDictionary<string, decimal> queryWeights, KeywordBaselineOptions options) =>
        ScorePreparedCore(resumeTerms, queryWeights, options);

    private static KeywordBaselineScore ScorePreparedCore(IReadOnlySet<string> resumeTerms,
        IReadOnlyDictionary<string, decimal> queryWeights, KeywordBaselineOptions options)
    {
        if (queryWeights.Count == 0) return new(null, 0, 0, []);

        var matched = queryWeights.Where(x => resumeTerms.Contains(x.Key))
            .OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).ToArray();
        var totalWeight = queryWeights.Values.Sum();
        var matchedWeight = matched.Sum(x => x.Value);
        double? score = totalWeight == 0 ? null : (double)Math.Round(matchedWeight / totalWeight, 6, MidpointRounding.AwayFromZero);
        return new(score, matched.Length, queryWeights.Count,
            matched.Take(options.ExplanationTermCount).Select(x => x.Key).ToArray());
    }

    public static string ConfigurationHash(KeywordBaselineOptions options)
    {
        Validate(options);
        var canonical = string.Join('\n',
            $"method={options.MethodVersion}",
            $"tokenizer={TokenizerVersion}",
            $"default_top_k={options.DefaultTopK}",
            $"maximum_top_k={options.MaximumTopK}",
            $"maximum_query_terms={options.MaximumQueryTerms}",
            $"explanation_term_count={options.ExplanationTermCount}",
            $"minimum_token_length={options.MinimumTokenLength}",
            $"title_weight={options.TitleWeight.ToString(CultureInfo.InvariantCulture)}",
            $"requirements_weight={options.RequirementsWeight.ToString(CultureInfo.InvariantCulture)}",
            $"description_weight={options.DescriptionWeight.ToString(CultureInfo.InvariantCulture)}",
            $"stop_words={string.Join(',', options.StopWords.Select(NormalizeToken).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    public static void Validate(KeywordBaselineOptions options)
    {
        if (options is null || options.StopWords is null || string.IsNullOrWhiteSpace(options.MethodVersion) || options.MethodVersion.Length > 80 ||
            options.DefaultTopK is < 1 or > 100 || options.MaximumTopK < options.DefaultTopK || options.MaximumTopK > 100 ||
            options.MaximumQueryTerms is < 1 or > 2000 || options.ExplanationTermCount is < 0 or > 32 ||
            options.MinimumTokenLength is < 1 or > 8 || options.TitleWeight <= 0 ||
            options.RequirementsWeight <= 0 || options.DescriptionWeight <= 0 ||
            options.StopWords.Length > 500 || options.StopWords.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 40))
            throw new InvalidOperationException("invalid_keyword_baseline_configuration");
    }

    private static void AddTerms(Dictionary<string, decimal> weights, string text, decimal weight,
        HashSet<string> stopWords, KeywordBaselineOptions options)
    {
        foreach (var term in ExtractTerms(text, stopWords, options.MinimumTokenLength, options.MaximumQueryTerms))
        {
            if (weights.TryGetValue(term, out var existing))
            {
                if (existing < weight) weights[term] = weight;
                continue;
            }
            if (weights.Count >= options.MaximumQueryTerms) return;
            weights[term] = weight;
        }
    }

    private static IEnumerable<string> ExtractTerms(string text, HashSet<string> stopWords, int minimumLength, int maximumTerms)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in Tokens.Matches(text.Normalize(NormalizationForm.FormKC)))
        {
            var token = NormalizeToken(match.Value);
            if (token.Length < minimumLength || stopWords.Contains(token) || !seen.Add(token)) continue;
            yield return token;
            if (seen.Count >= maximumTerms) yield break;
        }
    }

    private static string NormalizeToken(string value)
    {
        var formD = value.Normalize(NormalizationForm.FormD);
        var withoutMarks = new StringBuilder(formD.Length);
        foreach (var character in formD)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                withoutMarks.Append(character);
        return withoutMarks.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
