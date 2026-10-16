using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;

namespace CareerGap.Api.Files;

public sealed record ResumeStructuredSkill(string Name);
public sealed record ResumeStructuredSkills(int SchemaVersion, IReadOnlyList<ResumeStructuredSkill> Skills);
public sealed record ResumeSkillEvidence(string SkillName, string SourceType, string? SegmentId,
    int? StartOffset, int? EndOffset, string? Quote, string? Statement);
public sealed record ConfirmResumeRevisionRequest(int BaseRevisionNo,
    IReadOnlyList<string>? AcceptedSkillNames, IReadOnlyList<string>? UserStatementSkills);
public sealed record ResumeBuilderExperience(string Role, string Organization, string Period, string Description);
public sealed record ResumeBuilderEducation(string Program, string Institution, string Period, string Details);
public sealed record ResumeBuilderProject(string Name, string Context, string Description);
public sealed record ResumeBuilderContentRequest(int BaseRevisionNo, string TemplateVersion, string DisplayName,
    string? Email, string? Phone, string? Location, string? Headline, string? Summary,
    IReadOnlyList<string>? Skills, IReadOnlyList<ResumeBuilderExperience>? Experience,
    IReadOnlyList<ResumeBuilderEducation>? Education, IReadOnlyList<ResumeBuilderProject>? Projects,
    IReadOnlyList<string>? Certifications);
public sealed record ResumeBuilderStructuredContent(int SchemaVersion, string TemplateVersion, string DisplayName,
    string? Email, string? Phone, string? Location, string? Headline, string? Summary,
    IReadOnlyList<string> Skills, IReadOnlyList<ResumeBuilderExperience> Experience,
    IReadOnlyList<ResumeBuilderEducation> Education, IReadOnlyList<ResumeBuilderProject> Projects,
    IReadOnlyList<string> Certifications);
public sealed record ResumeBuilderEvidence(string Field, string SourceType);
public sealed record PreparedResumeBuilderRevision(string StructuredData, string SourceText, string Evidence,
    string DataHash, string ParserVersion);

public static class ResumeBuilderRevisionFactory
{
    public const int SchemaVersion = 2;
    public const string TemplateVersion = "simple-v1";
    public const string ParserVersion = "cv-template-simple-v1";

    public static bool TryPrepare(ResumeBuilderContentRequest request,
        out PreparedResumeBuilderRevision? prepared, out string? error)
    {
        prepared = null;
        error = null;
        if (request.BaseRevisionNo < 0 || request.TemplateVersion != TemplateVersion ||
            request.Skills is null || request.Experience is null || request.Education is null ||
            request.Projects is null || request.Certifications is null || request.Skills.Count > 20 ||
            request.Experience.Count > 10 || request.Education.Count > 10 || request.Projects.Count > 10 ||
            request.Certifications.Count > 20 || request.Experience.Any(item => item is null) ||
            request.Education.Any(item => item is null) || request.Projects.Any(item => item is null))
        { error = "invalid_resume_builder_revision"; return false; }

        static string? Optional(string? value, int maxLength)
        {
            var trimmed = value?.Trim();
            if (trimmed is { Length: > 0 } && trimmed.Length > maxLength) throw new ArgumentException();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
        static string Required(string? value, int maxLength)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > maxLength) throw new ArgumentException();
            return trimmed;
        }
        static string[] Lines(IReadOnlyList<string> values, int maxCount, int maxLength)
        {
            if (values.Count > maxCount) throw new ArgumentException();
            var result = new List<string>(values.Count);
            foreach (var value in values)
            {
                var trimmed = value?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > maxLength ||
                    result.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException();
                result.Add(trimmed);
            }
            return result.ToArray();
        }

        try
        {
            var displayName = Required(request.DisplayName, 150);
            var email = Optional(request.Email, 254);
            var phone = Optional(request.Phone, 50);
            var location = Optional(request.Location, 150);
            var headline = Optional(request.Headline, 200);
            var summary = Optional(request.Summary, 3000);
            if (email is not null && (!System.Net.Mail.MailAddress.TryCreate(email, out var address) || address!.Address != email))
            { error = "invalid_resume_builder_revision"; return false; }

            var skills = Lines(request.Skills, 20, 100);
            var experience = request.Experience.Select(item => new ResumeBuilderExperience(
                Required(item.Role, 160), Required(item.Organization, 160), Optional(item.Period, 100) ?? "",
                Required(item.Description, 1200))).ToArray();
            var education = request.Education.Select(item => new ResumeBuilderEducation(
                Required(item.Program, 180), Required(item.Institution, 180), Optional(item.Period, 100) ?? "",
                Optional(item.Details, 600) ?? "")).ToArray();
            var projects = request.Projects.Select(item => new ResumeBuilderProject(
                Required(item.Name, 180), Optional(item.Context, 160) ?? "", Required(item.Description, 1200))).ToArray();
            var certifications = Lines(request.Certifications, 20, 200);
            if (!skills.Any() && !experience.Any() && !education.Any() && !projects.Any() &&
                string.IsNullOrWhiteSpace(headline) && string.IsNullOrWhiteSpace(summary))
            { error = "resume_builder_content_required"; return false; }

            var data = new ResumeBuilderStructuredContent(SchemaVersion, TemplateVersion, displayName, email,
                phone, location, headline, summary, skills, experience, education, projects, certifications);
            var structured = JsonSerializer.Serialize(data, OutputValidator.Json);
            var source = JsonSerializer.Serialize(new { source = "candidate_entered", templateVersion = TemplateVersion }, OutputValidator.Json);
            var fields = new List<ResumeBuilderEvidence>();
            void AddField(string path, bool present) { if (present) fields.Add(new(path, "user_statement")); }
            AddField("displayName", true); AddField("email", email is not null); AddField("phone", phone is not null);
            AddField("location", location is not null); AddField("headline", headline is not null); AddField("summary", summary is not null);
            for (var i = 0; i < skills.Length; i++) AddField($"skills[{i}]", true);
            for (var i = 0; i < experience.Length; i++) { AddField($"experience[{i}].role", true); AddField($"experience[{i}].organization", true); AddField($"experience[{i}].period", !string.IsNullOrEmpty(experience[i].Period)); AddField($"experience[{i}].description", true); }
            for (var i = 0; i < education.Length; i++) { AddField($"education[{i}].program", true); AddField($"education[{i}].institution", true); AddField($"education[{i}].period", !string.IsNullOrEmpty(education[i].Period)); AddField($"education[{i}].details", !string.IsNullOrEmpty(education[i].Details)); }
            for (var i = 0; i < projects.Length; i++) { AddField($"projects[{i}].name", true); AddField($"projects[{i}].context", !string.IsNullOrEmpty(projects[i].Context)); AddField($"projects[{i}].description", true); }
            for (var i = 0; i < certifications.Length; i++) AddField($"certifications[{i}]", true);
            var evidence = JsonSerializer.Serialize(fields, OutputValidator.Json);
            if (Encoding.UTF8.GetByteCount(structured) > 32 * 1024)
            { error = "resume_builder_content_too_large"; return false; }
            var hashInput = Encoding.UTF8.GetBytes(string.Join("\n", SchemaVersion, ParserVersion, structured, source, evidence));
            prepared = new(structured, source, evidence, Convert.ToHexStringLower(SHA256.HashData(hashInput)), ParserVersion);
            return true;
        }
        catch (ArgumentException)
        { error = "invalid_resume_builder_revision"; return false; }
    }

    public static ResumeDataRevision Create(Guid candidateId, Guid versionId, Guid? parentId,
        int revisionNo, PreparedResumeBuilderRevision prepared, DateTimeOffset now) => new()
    {
        CandidateId = candidateId,
        ResumeVersionId = versionId,
        ParentRevisionId = parentId,
        RevisionNo = revisionNo,
        Origin = "user_entered",
        SchemaVersion = SchemaVersion,
        StructuredData = prepared.StructuredData,
        SourceText = prepared.SourceText,
        Evidence = prepared.Evidence,
        DataHash = prepared.DataHash,
        ParserVersion = prepared.ParserVersion,
        ConfirmedBy = candidateId,
        ConfirmedAt = now,
        CreatedAt = now
    };
}

public static class ResumeRevisionFactory
{
    public const int SchemaVersion = 1;
    public const string StructuredParserVersion = "skills-evidence-v1";

    public static ResumeDataRevision FromAi(Guid candidateId, Guid versionId, Guid? parentId, int revisionNo,
        Guid methodId, Guid jobId, ParsedDocument document, SkillOutput output, DateTimeOffset now)
    {
        var evidence = output.Skills.Select(skill =>
        {
            var segment = document.Segments.Single(x => x.Id == skill.EvidenceRef);
            var start = segment.Text.IndexOf(skill.Quote, StringComparison.Ordinal);
            if (start < 0) throw new InvalidOperationException("validated_evidence_missing");
            return new ResumeSkillEvidence(skill.Name, "document_span", segment.Id, start,
                start + skill.Quote.Length, skill.Quote, null);
        }).ToArray();
        var structured = JsonSerializer.Serialize(new ResumeStructuredSkills(SchemaVersion,
            output.Skills.Select(x => new ResumeStructuredSkill(x.Name)).ToArray()), OutputValidator.Json);
        var sourceText = JsonSerializer.Serialize(document.Segments, OutputValidator.Json);
        var evidenceJson = JsonSerializer.Serialize(evidence, OutputValidator.Json);
        return Create(candidateId, versionId, parentId, revisionNo, "ai_extracted", structured, sourceText,
            evidenceJson, DocumentParser.Version + "+" + StructuredParserVersion, methodId, jobId, null, null, now);
    }

    public static bool TryConfirm(ResumeDataRevision parent, Guid candidateId, int revisionNo,
        ConfirmResumeRevisionRequest request, DateTimeOffset now, out ResumeDataRevision? revision, out string? error)
    {
        revision = null;
        error = null;
        if (parent.Origin != "ai_extracted" || request.BaseRevisionNo != parent.RevisionNo ||
            request.AcceptedSkillNames is null || request.AcceptedSkillNames.Count > 20 ||
            request.UserStatementSkills is null || request.UserStatementSkills.Count > 20)
        { error = "invalid_revision_confirmation"; return false; }

        ResumeStructuredSkills? data;
        ResumeSkillEvidence[]? sourceEvidence;
        try
        {
            data = JsonSerializer.Deserialize<ResumeStructuredSkills>(parent.StructuredData, OutputValidator.Json);
            sourceEvidence = JsonSerializer.Deserialize<ResumeSkillEvidence[]>(parent.Evidence, OutputValidator.Json);
        }
        catch (JsonException) { error = "revision_data_unavailable"; return false; }
        if (data?.SchemaVersion != SchemaVersion || data.Skills is null || data.Skills.Count > 20 || sourceEvidence is null || sourceEvidence.Length > 20)
        { error = "revision_data_unavailable"; return false; }

        var extracted = data.Skills.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var accepted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in request.AcceptedSkillNames)
        {
            var name = raw?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || !extracted.Contains(name) || !accepted.Add(name))
            { error = "invalid_revision_confirmation"; return false; }
        }

        var additions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in request.UserStatementSkills)
        {
            var name = raw?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || accepted.Contains(name) || !additions.Add(name))
            { error = "invalid_revision_confirmation"; return false; }
        }
        if (accepted.Count + additions.Count > 20)
        { error = "invalid_revision_confirmation"; return false; }

        var chosenEvidence = sourceEvidence.Where(x => accepted.Contains(x.SkillName)).ToList();
        foreach (var name in additions)
            chosenEvidence.Add(new ResumeSkillEvidence(name, "user_statement", null, null, null, null, name));
        var structured = JsonSerializer.Serialize(new ResumeStructuredSkills(SchemaVersion,
            accepted.Select(x => new ResumeStructuredSkill(data.Skills.Single(s => string.Equals(s.Name, x, StringComparison.OrdinalIgnoreCase)).Name))
                .Concat(additions.Select(x => new ResumeStructuredSkill(x))).ToArray()), OutputValidator.Json);
        var evidence = JsonSerializer.Serialize(chosenEvidence, OutputValidator.Json);
        revision = Create(candidateId, parent.ResumeVersionId, parent.Id, revisionNo, "user_confirmed", structured,
            parent.SourceText, evidence, parent.ParserVersion, null, null, candidateId, now, now);
        return true;
    }

    private static ResumeDataRevision Create(Guid candidateId, Guid versionId, Guid? parentId, int revisionNo,
        string origin, string structured, string sourceText, string evidence, string parserVersion, Guid? methodId,
        Guid? jobId, Guid? confirmedBy, DateTimeOffset? confirmedAt, DateTimeOffset now) => new()
    {
        CandidateId = candidateId,
        ResumeVersionId = versionId,
        ParentRevisionId = parentId,
        RevisionNo = revisionNo,
        Origin = origin,
        SchemaVersion = SchemaVersion,
        StructuredData = structured,
        SourceText = sourceText,
        Evidence = evidence,
        DataHash = Hash(structured, sourceText, evidence, parserVersion),
        ParserVersion = parserVersion,
        MethodVersionId = methodId,
        AiJobId = jobId,
        ConfirmedBy = confirmedBy,
        ConfirmedAt = confirmedAt,
        CreatedAt = now
    };

    private static string Hash(string structured, string sourceText, string evidence, string parserVersion)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Join("\n", SchemaVersion, parserVersion, structured, sourceText, evidence));
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
