namespace CareerGap.Api.Data;
public sealed class ContentReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReporterId { get; set; }
    public string TargetType { get; set; } = "job";
    public Guid? TargetId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "open";
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class AdminAudit
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ActorId { get; set; }
    public Guid? TargetId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class EmailDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = "Resend";
    public string Status { get; set; } = "sent";
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
