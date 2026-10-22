using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CareerGap.Api.Data;

namespace CareerGap.Api.Recruitment;

public sealed record JobStructuredRequirement(string RequirementId, string Text, string Importance, string SourceType, decimal? MinimumYears = null, string Relation = "single", IReadOnlyList<string>? Alternatives = null, bool NeedsReview = false);
public sealed record JobStructuredRequirements(int SchemaVersion, IReadOnlyList<JobStructuredRequirement> Requirements);
public sealed record JobRequirementEvidence(string RequirementId, string SourceType, int? StartOffset,
    int? EndOffset, string? Quote, string? Statement);
public sealed record JobRequirementSelection(string RequirementId, string Importance);
public sealed record UserJobRequirement(string? Text, string? Importance);
public sealed record ConfirmJobRevisionRequest(int BaseRevisionNo,
    IReadOnlyList<JobRequirementSelection>? AcceptedRequirements, IReadOnlyList<UserJobRequirement>? UserStatements);

public static partial class JobRevisionFactory
{
    public const int SchemaVersion = 2;
    public const string ParserVersion = "jd-atomic-requirements-v3";
    public const int MaxRequirements = 100;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> ImportanceValues = new(StringComparer.Ordinal)
        { "required", "optional", "unspecified" };

    [GeneratedRegex(@"\b(optional|nice[\s-]+to[\s-]+have|preferred|preferably|bonus|plus)\b|không\s+(bắt\s+buộc|yêu\s+cầu|cần)|ưu\s+tiên|là\s+(?:(?:một|1)\s+)?lợi\s+thế|khuyến\s+khích", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OptionalHint();

    [GeneratedRegex(@"\b(required|must(?:\s+have)?|mandatory|minimum|at\s+least)\b|bắt\s+buộc|cần\s+có|yêu\s+cầu|tối\s+thiểu", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RequiredHint();

    public static bool TryExtract(Guid versionId, Guid? parentId, int revisionNo, string requirementsText,
        DateTimeOffset now, out JobDataRevision? revision, out string? error)
    {
        revision = null;
        error = null;
        var requirements = new List<JobStructuredRequirement>();
        var evidence = new List<JobRequirementEvidence>();
        var ordinal = 0;
        var sectionImportance = "unspecified";
        foreach (var (line, start) in Lines(requirementsText))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (TrySectionHeader(trimmed, out var headerImportance))
            { sectionImportance = headerImportance; continue; }
            // Unknown Markdown headings end the preceding scope, but are not data.
            if (trimmed.StartsWith('#')) { sectionImportance = "unspecified"; continue; }
            var bullet = Regex.Match(line, @"^\s*(?:[-*•]|\d+[.)])\s+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            var bodyOffset = bullet.Success ? bullet.Length : 0;
            var body = line[bodyOffset..];
            var prefix = Regex.Match(body.TrimStart(), @"^(?:required|mandatory|must\s+have|optional|preferred|ưu\s+tiên|bắt\s+buộc)\s*:", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
            var lineImportance = prefix.Success ? HintImportance(prefix.Value) : sectionImportance;
            var pieces = SplitAtoms(body).ToArray();
            foreach (var piece in pieces)
            {
                var quote = piece.Text.Trim(); if (quote.Length == 0) continue;
                var offset = start + bodyOffset + piece.Offset + piece.Text.Length - piece.Text.TrimStart().Length;
                var localImportance = HintImportance(quote);
                var optional = OptionalHint().IsMatch(quote);
                var required = RequiredHint().IsMatch(quote);
                var importance = optional || required ? localImportance : lineImportance;
                var disjunction = Disjunction(quote);
                var id = $"source-{++ordinal:D4}";
                var years = Regex.Matches(quote, @"(?<![\d.])(?<min>\d{1,2}(?:\.\d+)?)\s*(?:[-–]\s*\d{1,2})?\+?\s*(?:years?|năm)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                var noExperience = Regex.IsMatch(quote, @"không\s+(?:yêu\s+cầu|cần)(?:\s+có)?\s+kinh\s+nghiệm|\bno\s+(?:prior\s+)?experience\s+(?:required|necessary)\b|\bexperience\s+(?:is\s+)?not\s+required\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                var ambiguousYears = years.Count > 1 || (years.Count > 0 && noExperience);
                decimal? minimum = !ambiguousYears && years.Count == 1 && decimal.TryParse(years[0].Groups["min"].Value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var parsedYears) && parsedYears <= 99.99m ? parsedYears : null;
                var alternatives = disjunction ? Regex.Split(quote, @"\s+(?:and/or|or|hoặc)\s+(?!(?:more|above|higher|cao\s+hơn|trở\s+lên)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)).Select(x => x.Trim()).ToArray() : null;
                var examples = Regex.IsMatch(quote, @"\([^)]*[,/][^)]*\)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                var conjunction = Regex.IsMatch(quote, @"\s+(?:and|và)\s+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
                requirements.Add(new(id, quote, importance, "document_text", minimum, disjunction ? "any_of" : "single", alternatives, disjunction || pieces.Length > 1 || ambiguousYears || examples || conjunction || (optional && required)));
                evidence.Add(new(id, "document_text", offset, offset + quote.Length, quote, null));
            }
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
        if (parsed?.SchemaVersion is not (1 or SchemaVersion) || parsed.Requirements is null ||
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

    private static bool TrySectionHeader(string line, out string importance)
    {
        var key = Regex.Replace(line.Trim(' ', '\t', ':', '-', '#').ToLowerInvariant(), @"[\s-]+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        importance = key switch
        {
            "required" or "mandatory" or "required skills" or "required qualifications" or "required technical skills" or "required skills and technologies" or "yêu cầu bắt buộc" or "bắt buộc" => "required",
            "optional" or "preferred" or "preferred qualifications" or "nice to have" or "nice to have qualifications" or "desired qualities" or "ưu tiên" => "optional",
            _ => "unspecified"
        };
        return importance != "unspecified" || key is "requirements" or "qualifications" or "what we need" or "yêu cầu" or "kỹ năng yêu cầu" or "điều kiện" or "experience profile" or "primary stack" or "secondary technologies" or "core qualifications" or "work environment";
    }

    private static bool Disjunction(string text) => Regex.IsMatch(text, @"\s+(?:and/or|or|hoặc)\s+(?!(?:more|above|higher|cao\s+hơn|trở\s+lên)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));

    private static IEnumerable<(string Text, int Offset)> SplitAtoms(string text)
    {
        // Split sentences/semicolons first. Parenthetical examples, decimals and
        // technology names such as Nest.js/.NET remain in their original clause.
        var clauses = new List<(int Start, int End)>(); var start = 0; var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            if (text[i] == ')' && depth > 0) depth--;
            if (depth != 0) continue;
            if (text[i] == ';') { clauses.Add((start, i)); start = i + 1; }
            else if (text[i] is '.' or '!' or '?' && i + 2 < text.Length && char.IsWhiteSpace(text[i + 1]))
            {
                var next = i + 1; while (next < text.Length && char.IsWhiteSpace(text[next])) next++;
                if (next < text.Length && char.IsUpper(text[next])) { clauses.Add((start, i + 1)); start = i + 1; }
            }
        }
        clauses.Add((start, text.Length));
        foreach (var clause in clauses)
        {
            var value = text[clause.Start..clause.End];
            // A or B stays together; commas inside examples never create criteria.
            if (Disjunction(value)) { yield return (value, clause.Start); continue; }
            var atom = clause.Start; depth = 0;
            for (var i = clause.Start; i < clause.End; i++)
            {
                if (text[i] == '(') depth++;
                if (text[i] == ')' && depth > 0) depth--;
                if (text[i] == ',' && depth == 0) { yield return (text[atom..i], atom); atom = i + 1; }
            }
            yield return (text[atom..clause.End], atom);
        }
    }

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
