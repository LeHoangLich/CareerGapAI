using System.Text.Json;
using System.Text.RegularExpressions;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Files;

namespace CareerGap.Api.Matching;

public static class ResumeSkillProjection
{
    public const string Version = "owner-confirmed-skills-v2-aliases-v1";
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    { ["reactjs"] = "react", ["react.js"] = "react", ["nodejs"] = "node.js" };
    public static HashSet<string> CanonicalTerms(IEnumerable<string> terms) => terms.Select(term => Aliases.GetValueOrDefault(term) ?? term).ToHashSet(StringComparer.Ordinal);

    public static bool TryRead(ResumeDataRevision revision, bool publicScope, out List<string> skills)
    {
        skills = [];
        if (revision.ConfirmedBy != revision.CandidateId || revision.ConfirmedAt is null) return false;
        if (revision.SchemaVersion == 3 && revision.Origin == "user_confirmed")
        {
            if (!FullCvExtraction.TryRead(revision, out var fields, out var evidence)) return false;
            var map = evidence.ToDictionary(x => x.Path, StringComparer.Ordinal);
            skills = fields.Where(x => Regex.IsMatch(x.Path, @"^skills\[\d+\]$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)) &&
                (!publicScope || map[x.Path].SourceType == "document_span")).Select(x => x.Value).ToList();
            return true;
        }
        if (revision.SchemaVersion != 2 || revision.Origin != "user_entered") return false;
        try
        {
            var content = JsonSerializer.Deserialize<ResumeBuilderStructuredContent>(revision.StructuredData, OutputValidator.Json);
            if (content is null || content.SchemaVersion != 2) return false;
            var input = new ResumeBuilderContentRequest(0, content.TemplateVersion, content.DisplayName, content.Email,
                content.Phone, content.Location, content.Headline, content.Summary, content.Skills, content.Experience,
                content.Education, content.Projects, content.Certifications);
            if (!ResumeBuilderRevisionFactory.TryPrepare(input, out var prepared, out _) || prepared!.StructuredData != revision.StructuredData || prepared.Evidence != revision.Evidence) return false;
            if (!publicScope) skills = content.Skills.ToList();
            return true;
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or NullReferenceException) { return false; }
    }
}
