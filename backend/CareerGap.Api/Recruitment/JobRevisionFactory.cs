using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CareerGap.Api.Data;

namespace CareerGap.Api.Recruitment;

public sealed record JobStructuredRequirement(string RequirementId, string Text, string Importance, string SourceType);
public sealed record JobStructuredRequirements(int SchemaVersion, IReadOnlyList<JobStructuredRequirement> Requirements);
public sealed record JobRequirementEvidence(string RequirementId, string SourceType, int? StartOffset,
    int? EndOffset, string? Quote, string? Statement);
public sealed record JobRequirementSelection(string RequirementId, string Importance);
public sealed record UserJobRequirement(string? Text, string? Importance);
public sealed record ConfirmJobRevisionRequest(int BaseRevisionNo,
    IReadOnlyList<JobRequirementSelection>? AcceptedRequirements, IReadOnlyList<UserJobRequirement>? UserStatements);

public static partial class JobRevisionFactory
{
    public const int SchemaVersion = 1;
    public const string ParserVersion = "jd-lines-importance-hints-v1";
    public const int MaxRequirements = 100;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ImportanceValues = new(StringComparer.Ordinal)
        { "required", "optional", "unspecified" };

    [GeneratedRegex(@"\b(optional|nice\s+to\s+have|preferred|bonus|plus)\b|không\s+(bắt\s+buộc|yêu\s+cầu|cần)|ưu\s+tiên|là\s+lợi\s+thế|khuyến\s+khích", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OptionalHint();

    [GeneratedRegex(@"\b(required|must(?:\s+have)?|mandatory)\b|bắt\s+buộc|cần\s+có|yêu\s+cầu|tối\s+thiểu", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RequiredHint();

    public static bool TryExtract(Guid versionId, Guid? parentId, int revisionNo, string requirementsText,
        DateTimeOffset now, out JobDataRevision? revision, out string? error)
    {
        revision = null;
        error = null;
        var requirements = new List<JobStructuredRequirement>();
        var evidence = new List<JobRequirementEvidence>();
        var ordinal = 0;
        foreach (var (line, start) in Lines(requirementsText))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || IsSectionHeader(trimmed)) continue;
            var leading = line.Length - line.TrimStart().Length;
            var quote = line.Trim();
            var offset = start + leading;
            var id = $"source-{++ordinal:D4}";
            requirements.Add(new JobStructuredRequirement(id, quote, HintImportance(quote), "document_text"));
            evidence.Add(new JobRequirementEvidence(id, "document_text", offset, offset + quote.Length, quote, null));
        }
        if (requirements.Count > MaxRequirements) { error = "job_requirement_limit"; return false; }

        var sourceText = JsonSerializer.Serialize(new { requirementsText }, Json);
        revision = Create(versionId, revisionNo, parentId, "local_extracted", requirements, sourceText, evidence,
            ParserVersion, null, null, now);
        return true;
    }

    public static bool TryConfirm(JobDataRevision parent, Guid ownerId, int revisionNo,
        ConfirmJobRevisionRequest request, DateTimeOffset now, out JobDataRevision? revision, out string? error)
    {
        revision = null;
        error = null;
        if (parent.Origin != "local_extracted" || request.BaseRevisionNo != parent.RevisionNo ||
            request.AcceptedRequirements is null || request.UserStatements is null ||
            request.AcceptedRequirements.Count > MaxRequirements || request.UserStatements.Count > MaxRequirements ||
            request.AcceptedRequirements.Count + request.UserStatements.Count > MaxRequirements)
        { error = "invalid_job_revision_confirmation"; return false; }

        JobStructuredRequirements? parsed;
        JobRequirementEvidence[]? parentEvidence;
        try
        {
            parsed = JsonSerializer.Deserialize<JobStructuredRequirements>(parent.StructuredData, Json);
            parentEvidence = JsonSerializer.Deserialize<JobRequirementEvidence[]>(parent.Evidence, Json);
        }
        catch (JsonException) { error = "job_revision_data_unavailable"; return false; }
        if (parsed?.SchemaVersion != SchemaVersion || parsed.Requirements is null ||
            parsed.Requirements.Count > MaxRequirements || parentEvidence is null || parentEvidence.Length > MaxRequirements)
        { error = "job_revision_data_unavailable"; return false; }

        var sourceItems = parsed.Requirements.ToDictionary(x => x.RequirementId, StringComparer.Ordinal);
        var chosenIds = new HashSet<string>(StringComparer.Ordinal);
        var chosenTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var confirmed = new List<JobStructuredRequirement>();
        foreach (var selection in request.AcceptedRequirements)
        {
            if (selection is null || !sourceItems.TryGetValue(selection.RequirementId, out var source) ||
                !ImportanceValues.Contains(selection.Importance) || !chosenIds.Add(selection.RequirementId) ||
                !chosenTexts.Add(source.Text))
            { error = "invalid_job_revision_confirmation"; return false; }
            confirmed.Add(source with { Importance = selection.Importance });
        }

        var evidence = parentEvidence.Where(x => chosenIds.Contains(x.RequirementId)).ToList();
        var manualOrdinal = 0;
        foreach (var item in request.UserStatements)
        {
            var text = item?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text) || text.Length > 2000 || item?.Importance is null ||
                !ImportanceValues.Contains(item.Importance) || !chosenTexts.Add(text))
            { error = "invalid_job_revision_confirmation"; return false; }
            var id = $"manual-{++manualOrdinal:D4}";
            confirmed.Add(new JobStructuredRequirement(id, text, item.Importance, "user_statement"));
            evidence.Add(new JobRequirementEvidence(id, "user_statement", null, null, null, text));
        }

        revision = Create(parent.JobVersionId, revisionNo, parent.Id, "user_confirmed", confirmed,
            parent.SourceText, evidence, parent.ParserVersion, ownerId, now, now);
        return true;
    }

    private static JobDataRevision Create(Guid versionId, int revisionNo, Guid? parentId, string origin,
        IReadOnlyList<JobStructuredRequirement> requirements, string sourceText,
        IReadOnlyList<JobRequirementEvidence> evidence, string parserVersion, Guid? confirmedBy,
        DateTimeOffset? confirmedAt, DateTimeOffset now)
    {
        var structured = JsonSerializer.Serialize(new JobStructuredRequirements(SchemaVersion, requirements), Json);
        var evidenceJson = JsonSerializer.Serialize(evidence, Json);
        return new JobDataRevision
        {
            JobVersionId = versionId,
            RevisionNo = revisionNo,
            ParentRevisionId = parentId,
            Origin = origin,
            SchemaVersion = SchemaVersion,
            StructuredData = structured,
            SourceText = sourceText,
            Evidence = evidenceJson,
            DataHash = Hash(origin, structured, sourceText, evidenceJson, parserVersion),
            ParserVersion = parserVersion,
            ConfirmedBy = confirmedBy,
            ConfirmedAt = confirmedAt,
            CreatedAt = now
        };
    }

    private static string Hash(string origin, string structured, string sourceText, string evidence, string parserVersion) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n", origin, SchemaVersion, parserVersion, structured, sourceText, evidence))));

    private static string HintImportance(string line)
    {
        var optional = OptionalHint().IsMatch(line);
        var required = RequiredHint().IsMatch(line);
        return required == optional ? "unspecified" : required ? "required" : "optional";
    }

    private static bool IsSectionHeader(string line) => line.Trim(' ', '\t', ':', '-', '#').ToLowerInvariant() is
        "requirements" or "qualifications" or "what we need" or "yêu cầu" or "kỹ năng yêu cầu" or "điều kiện";

    private static IEnumerable<(string Text, int Start)> Lines(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline < 0 ? text.Length : newline;
            if (end > start && text[end - 1] == '\r') end--;
            yield return (text[start..end], start);
            if (newline < 0) yield break;
            start = newline + 1;
        }
    }
}
