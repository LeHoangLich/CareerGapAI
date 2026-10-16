using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Recruitment;

namespace CareerGap.Api.Matching;

public sealed record ConfirmedRevisionDiagnostics(string MethodVersion, string Status,
    int? ResumeRevisionNo, int? JobRevisionNo, int? EligibleSkillCount, int? RequirementCount,
    int? RequiredRequirementCount, int? MatchedRequirementCount, int? MatchedRequiredRequirementCount,
    int? OptionalRequirementCount, int? UnspecifiedRequirementCount);

public static class ConfirmedRevisionMatcher
{
    public const string MethodVersion = "confirmed-revision-term-overlap-diagnostic-v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ConfirmedRevisionDiagnostics Evaluate(ResumeDataRevision? resumeRevision,
        JobDataRevision? jobRevision, KeywordBaselineOptions options, bool publicResumeScope)
    {
        if (resumeRevision is null)
            return Empty(jobRevision is null ? "resume_and_job_revision_unconfirmed" : "resume_revision_unconfirmed",
                jobRevisionNo: jobRevision?.RevisionNo);
        if (jobRevision is null)
            return Empty("job_revision_unconfirmed", resumeRevision.RevisionNo);
        if (!TryReadResume(resumeRevision, publicResumeScope, out var skills))
            return Empty("resume_revision_data_unavailable", resumeRevision.RevisionNo, jobRevision.RevisionNo);
        if (!TryReadJob(jobRevision, out var requirements))
            return Empty("job_revision_data_unavailable", resumeRevision.RevisionNo, jobRevision.RevisionNo);
        if (skills.Count == 0)
            return Empty(publicResumeScope ? "no_public_document_skills" : "resume_revision_empty",
                resumeRevision.RevisionNo, jobRevision.RevisionNo, 0, requirements.Count, requirements);
        if (requirements.Count == 0)
            return Empty("job_revision_empty", resumeRevision.RevisionNo, jobRevision.RevisionNo,
                skills.Count, 0, requirements);

        var counts = CountRequirementOverlaps(skills, requirements, options);

        return new(MethodVersion, "ready", resumeRevision.RevisionNo, jobRevision.RevisionNo,
            skills.Count, requirements.Count, counts.Required, counts.Matched,
            counts.MatchedRequired, counts.Optional,
            requirements.Count(x => x.Importance == "unspecified"));
    }

    public static (int Matched, int Required, int MatchedRequired, int Optional) CountRequirementOverlaps(
        IReadOnlyList<string> skills, IReadOnlyList<JobStructuredRequirement> requirements,
        KeywordBaselineOptions options)
    {
        var skillTerms = skills.Select(x => KeywordBaselineScorer.PrepareResumeTerms(x, options)).ToArray();
        var matched = 0;
        var required = 0;
        var matchedRequired = 0;
        var optional = 0;
        foreach (var requirement in requirements)
        {
            if (requirement.Importance == "required") required++;
            if (requirement.Importance == "optional") optional++;
            var terms = KeywordBaselineScorer.PrepareResumeTerms(requirement.Text, options);
            if (terms.Count == 0 || !skillTerms.Any(skill => skill.Overlaps(terms))) continue;
            matched++;
            if (requirement.Importance == "required") matchedRequired++;
        }
        return (matched, required, matchedRequired, optional);
    }

    private static ConfirmedRevisionDiagnostics Empty(string status, int? resumeRevisionNo = null,
        int? jobRevisionNo = null, int? skillCount = null, int? requirementCount = null,
        IReadOnlyList<JobStructuredRequirement>? requirements = null) =>
        new(MethodVersion, status, resumeRevisionNo, jobRevisionNo, skillCount, requirementCount,
            requirements?.Count(x => x.Importance == "required"), null, null,
            requirements?.Count(x => x.Importance == "optional"),
            requirements?.Count(x => x.Importance == "unspecified"));

    private static bool TryReadResume(ResumeDataRevision revision, bool publicScope, out List<string> skills)
    {
        skills = [];
        if (revision.Origin != "user_confirmed" || revision.ConfirmedBy is null || revision.ConfirmedAt is null ||
            revision.SchemaVersion != ResumeRevisionFactory.SchemaVersion) return false;
        try
        {
            var data = JsonSerializer.Deserialize<ResumeStructuredSkills>(revision.StructuredData, Json);
            var evidence = JsonSerializer.Deserialize<ResumeSkillEvidence[]>(revision.Evidence, Json);
            var source = JsonSerializer.Deserialize<SourceSegment[]>(revision.SourceText, Json);
            if (data?.SchemaVersion != ResumeRevisionFactory.SchemaVersion || data.Skills is null ||
                data.Skills.Count > 20 || data.Skills.Any(x => x is null) || evidence is null ||
                evidence.Length != data.Skills.Count || evidence.Any(x => x is null) || source is null ||
                source.Any(x => x is null || x.Id is null || x.Text is null))
                return false;
            var sourceSegments = source.ToDictionary(x => x.Id, x => x.Text, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var evidenceBySkill = evidence.ToDictionary(x => x.SkillName, StringComparer.OrdinalIgnoreCase);
            foreach (var skill in data.Skills)
            {
                if (string.IsNullOrWhiteSpace(skill.Name) || skill.Name.Length > 100 || !seen.Add(skill.Name) ||
                    !evidenceBySkill.TryGetValue(skill.Name, out var item)) return false;
                if (item.SourceType == "document_span")
                {
                    if (item.SegmentId is null || item.StartOffset is null || item.EndOffset is null ||
                        string.IsNullOrEmpty(item.Quote) || item.Statement is not null ||
                        !sourceSegments.TryGetValue(item.SegmentId, out var segment) ||
                        item.StartOffset < 0 || item.EndOffset > segment.Length ||
                        item.EndOffset - item.StartOffset != item.Quote.Length ||
                        !segment.AsSpan(item.StartOffset.Value, item.Quote.Length).SequenceEqual(item.Quote.AsSpan())) return false;
                    skills.Add(skill.Name);
                }
                else if (item.SourceType == "user_statement")
                {
                    if (item.SegmentId is not null || item.StartOffset is not null || item.EndOffset is not null ||
                        item.Quote is not null || item.Statement != skill.Name) return false;
                    if (!publicScope) skills.Add(skill.Name);
                }
                else return false;
            }
            return true;
        }
        catch (JsonException) { return false; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return false; }
    }

    private static bool TryReadJob(JobDataRevision revision, out List<JobStructuredRequirement> requirements)
    {
        requirements = [];
        if (revision.Origin != "user_confirmed" || revision.ConfirmedBy is null || revision.ConfirmedAt is null ||
            revision.SchemaVersion != JobRevisionFactory.SchemaVersion) return false;
        try
        {
            var data = JsonSerializer.Deserialize<JobStructuredRequirements>(revision.StructuredData, Json);
            var evidence = JsonSerializer.Deserialize<JobRequirementEvidence[]>(revision.Evidence, Json);
            using var source = JsonDocument.Parse(revision.SourceText);
            var sourceText = source.RootElement.GetProperty("requirementsText").GetString();
            if (data?.SchemaVersion != JobRevisionFactory.SchemaVersion || data.Requirements is null ||
                data.Requirements.Count > JobRevisionFactory.MaxRequirements || data.Requirements.Any(x => x is null) ||
                evidence is null || evidence.Length != data.Requirements.Count || evidence.Any(x => x is null) ||
                sourceText is null) return false;
            var evidenceByRequirement = evidence.ToDictionary(x => x.RequirementId, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var requirement in data.Requirements)
            {
                if (string.IsNullOrWhiteSpace(requirement.RequirementId) || string.IsNullOrWhiteSpace(requirement.Text) ||
                    requirement.Text.Length > 2000 || requirement.Importance is not ("required" or "optional" or "unspecified") ||
                    !seen.Add(requirement.RequirementId) || !evidenceByRequirement.TryGetValue(requirement.RequirementId, out var item) ||
                    item.SourceType != requirement.SourceType) return false;
                if (item.SourceType == "document_text")
                {
                    if (item.StartOffset is null || item.EndOffset is null || item.Quote != requirement.Text ||
                        item.Statement is not null || item.StartOffset < 0 || item.EndOffset > sourceText.Length ||
                        item.EndOffset - item.StartOffset != requirement.Text.Length ||
                        !sourceText.AsSpan(item.StartOffset.Value, requirement.Text.Length).SequenceEqual(requirement.Text.AsSpan())) return false;
                }
                else if (item.SourceType == "user_statement")
                {
                    if (item.StartOffset is not null || item.EndOffset is not null || item.Quote is not null ||
                        item.Statement != requirement.Text) return false;
                }
                else return false;
                requirements.Add(requirement);
            }
            return true;
        }
        catch (JsonException) { return false; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { return false; }
    }
}
