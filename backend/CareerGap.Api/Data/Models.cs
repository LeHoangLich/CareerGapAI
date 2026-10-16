using Microsoft.AspNetCore.Identity;

namespace CareerGap.Api.Data;

public sealed class AppUser : IdentityUser<Guid>
{
    public string AccountStatus { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class AccountDeletionRequestRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Status { get; set; } = "pending";
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset PurgeAfter { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? DataPurgedAt { get; set; }
    public DateTimeOffset? PurgedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public long LeaseGeneration { get; set; }
    public string? LastErrorCode { get; set; }
}
public sealed class AccountDeletionObjectRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequestId { get; set; }
    public string Provider { get; set; } = "Local";
    public string ObjectKey { get; set; } = "";
    public string Status { get; set; } = "pending";
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
    public string? LastErrorCode { get; set; }
}
public sealed class CandidateProfile
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "Demo candidate";
    public string? Headline { get; set; }
    public string? Summary { get; set; }
    public string? Location { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? DesiredRole { get; set; }
    public string? PreferredWorkMode { get; set; }
    public decimal? ExperienceYears { get; set; }
    public string Details { get; set; } = "{}";
    public int DetailsSchemaVersion { get; set; } = 1;
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CandidateProfileVisibility
{
    public Guid CandidateId { get; set; }
    public string FieldKey { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Location { get; set; }
    public string? Industry { get; set; }
    public string? SizeLabel { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ArchivedAt { get; set; }
}
public sealed class JobPosting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public string Status { get; set; } = "draft";
    public Guid? CurrentVersionId { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset? ApplicationDeadline { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Company? Company { get; set; }
    public JobVersion? CurrentVersion { get; set; }
}
public sealed class JobVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public Guid CreatedBy { get; set; }
    public int VersionNo { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string RequirementsText { get; set; } = "";
    public string? BenefitsText { get; set; }
    public string? Industry { get; set; }
    public string? Location { get; set; }
    public string EmploymentType { get; set; } = "full_time";
    public string WorkMode { get; set; } = "unspecified";
    public decimal? ExperienceMin { get; set; }
    public decimal? ExperienceMax { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public string? SalaryPeriod { get; set; }
    public string Language { get; set; } = "vi";
    public string ContentHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public JobPosting? Job { get; set; }
}
public sealed class SavedJob
{
    public Guid CandidateId { get; set; }
    public Guid JobId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class ApplicationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public Guid JobId { get; set; }
    public Guid SubmittedResumeVersionId { get; set; }
    public Guid SubmittedJobVersionId { get; set; }
    public string Status { get; set; } = "submitted";
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset StatusChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? WithdrawnAt { get; set; }
    public Guid SubmissionKey { get; set; }
    public string SubmissionHash { get; set; } = "";
    public long RowVersion { get; set; }
}
public sealed class ApplicationStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "submitted";
    public Guid ActorUserId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? ReasonCode { get; set; }
}
public sealed class ApplicationNoteRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ApplicationId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class JobCandidateBlockRecord
{
    public Guid JobId { get; set; }
    public Guid CandidateId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class NotificationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecipientUserId { get; set; }
    public string NotificationType { get; set; } = "system_notice";
    public Guid? ApplicationId { get; set; }
    public Guid? AiJobId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAt { get; set; }
    public string DeduplicationKey { get; set; } = "";
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
    public int CleanupAttemptCount { get; set; }
    public DateTimeOffset? CleanupNextAttemptAt { get; set; }
    public DateTimeOffset? CleanupLeaseUntil { get; set; }
    public string? CleanupLastErrorCode { get; set; }
}
public sealed class Resume
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateId { get; set; }
    public string Title { get; set; } = "";
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
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
public sealed class ResumeDataRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ResumeVersionId { get; set; }
    public Guid CandidateId { get; set; }
    public int RevisionNo { get; set; }
    public Guid? ParentRevisionId { get; set; }
    public string Origin { get; set; } = "ai_extracted";
    public int SchemaVersion { get; set; } = 1;
    public string StructuredData { get; set; } = "{}";
    public string SourceText { get; set; } = "[]";
    public string Evidence { get; set; } = "[]";
    public string DataHash { get; set; } = "";
    public string ParserVersion { get; set; } = "";
    public Guid? MethodVersionId { get; set; }
    public Guid? AiJobId { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class JobDataRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobVersionId { get; set; }
    public int RevisionNo { get; set; }
    public Guid? ParentRevisionId { get; set; }
    public string Origin { get; set; } = "local_extracted";
    public int SchemaVersion { get; set; } = 1;
    public string StructuredData { get; set; } = "{}";
    public string SourceText { get; set; } = "{}";
    public string Evidence { get; set; } = "[]";
    public string DataHash { get; set; } = "";
    public string ParserVersion { get; set; } = "";
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class CandidatePublicResume
{
    public Guid CandidateId { get; set; }
    public Guid ResumeVersionId { get; set; }
    public Guid? RedactedFileId { get; set; }
    public string SourceHash { get; set; } = "";
    public string? RedactionMethodVersion { get; set; }
    public DateTimeOffset? RedactionVerifiedAt { get; set; }
    public string Status { get; set; } = "not_ready";
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
    public long RowVersion { get; set; }
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
    public Guid? ResumeVersionId { get; set; }
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
