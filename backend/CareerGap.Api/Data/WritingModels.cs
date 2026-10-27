namespace CareerGap.Api.Data;

public sealed class WritingInput
{
    public Guid AiJobId { get; set; }
    public Guid CandidateId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public Guid ResumeRevisionId { get; set; }
    public string Fingerprint { get; set; } = "";
    public string Payload { get; set; } = "{}";
}
public sealed class WritingResult
{
    public Guid AiJobId { get; set; }
    public string Sections { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class WritingDecision
{
    public Guid AiJobId { get; set; }
    public string Action { get; set; } = "discard";
    public string Sections { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
