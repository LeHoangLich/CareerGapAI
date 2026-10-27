namespace CareerGap.Api.Data;

public sealed class LearningRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public Guid? PairJobId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string InputHash { get; set; } = "";
    public string RequirementKey { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Language { get; set; } = "vi";
    public string Level { get; set; } = "beginner";
    public string Provider { get; set; } = "Fake";
    public string Status { get; set; } = "sent";
    public int ReservedCredits { get; set; }
    public int? ActualCredits { get; set; }
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public string? LinksJson { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
