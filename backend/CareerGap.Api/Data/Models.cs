using Microsoft.AspNetCore.Identity;

namespace CareerGap.Api.Data;

public sealed class AppUser : IdentityUser<Guid>
{
    public string AccountStatus { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CandidateProfile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "Demo candidate";
}
public sealed class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public string ObjectKey { get; set; } = "";
    public string Provider { get; set; } = "Local";
    public string OriginalName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public string Status { get; set; } = "pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? VerifiedAt { get; set; }
}
public sealed class Resume
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public string Title { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ResumeVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResumeId { get; set; }
    public Guid CandidateId { get; set; }
    public Guid FileId { get; set; }
    public int VersionNo { get; set; } = 1;
    public string DocumentHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class AiMethod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ConfigHash { get; set; } = "";
    public string Provider { get; set; } = "Fake";
    public string Model { get; set; } = "";
    public string PromptVersion { get; set; } = "extract-probe-v1";
    public string SchemaVersion { get; set; } = "skills-evidence-v1";
    public string Parameters { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class AiJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestedBy { get; set; }
    public Guid ResumeVersionId { get; set; }
    public Guid MethodVersionId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string InputHash { get; set; } = "";
    public string Status { get; set; } = "queued";
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 2;
    public long LeaseGeneration { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset AvailableAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAt { get; set; }
    public string? OutputJson { get; set; }
    public string? ErrorCode { get; set; }
}
public sealed class BudgetPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Service { get; set; } = "Fake";
    public string Unit { get; set; } = "test_credit";
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal ReservedAmount { get; set; }
}
public sealed class BudgetReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AiJobId { get; set; }
    public Guid BudgetPeriodId { get; set; }
    public decimal ReservedRemaining { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public decimal PerCallEstimate { get; set; }
}
public sealed class ExternalCallAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AiJobId { get; set; }
    public int CallNo { get; set; }
    public long LeaseGeneration { get; set; }
    public string Status { get; set; } = "prepared";
    public string PricingSnapshot { get; set; } = "{}";
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public decimal? ActualAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedAt { get; set; }
    public DateTimeOffset? ReconciledAt { get; set; }
}
public sealed class CallBudgetAllocation
{
    public Guid CallAttemptId { get; set; }
    public Guid BudgetReservationId { get; set; }
    public decimal ReservedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public string Status { get; set; } = "allocated";
}
