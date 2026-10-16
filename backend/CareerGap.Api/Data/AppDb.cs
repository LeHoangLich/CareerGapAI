using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareerGap.Api.Data;

public sealed class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<AccountDeletionRequestRecord> AccountDeletionRequests => Set<AccountDeletionRequestRecord>();
    public DbSet<AccountDeletionObjectRecord> AccountDeletionObjects => Set<AccountDeletionObjectRecord>();
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<CandidateProfileVisibility> CandidateProfileVisibility => Set<CandidateProfileVisibility>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<JobPosting> Jobs => Set<JobPosting>();
    public DbSet<JobVersion> JobVersions => Set<JobVersion>();
    public DbSet<SavedJob> SavedJobs => Set<SavedJob>();
    public DbSet<ApplicationRecord> Applications => Set<ApplicationRecord>();
    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory => Set<ApplicationStatusHistory>();
    public DbSet<ApplicationNoteRecord> ApplicationNotes => Set<ApplicationNoteRecord>();
    public DbSet<JobCandidateBlockRecord> JobCandidateBlocks => Set<JobCandidateBlockRecord>();
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<ResumeVersion> ResumeVersions => Set<ResumeVersion>();
    public DbSet<ResumeDataRevision> ResumeDataRevisions => Set<ResumeDataRevision>();
    public DbSet<JobDataRevision> JobDataRevisions => Set<JobDataRevision>();
    public DbSet<CandidatePublicResume> CandidatePublicResumes => Set<CandidatePublicResume>();
    public DbSet<AiMethod> AiMethods => Set<AiMethod>();
    public DbSet<AiJob> AiJobs => Set<AiJob>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<BudgetReservation> BudgetReservations => Set<BudgetReservation>();
    public DbSet<ExternalCallAttempt> ExternalCallAttempts => Set<ExternalCallAttempt>();
    public DbSet<CallBudgetAllocation> CallBudgetAllocations => Set<CallBudgetAllocation>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<AppUser>().Property(x => x.AccountStatus).HasMaxLength(24);
        b.Entity<AppUser>().HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("\"NormalizedEmail\" IS NOT NULL");
        b.Entity<CandidateProfile>().HasKey(x => x.UserId);
        b.Entity<CandidateProfile>().HasOne<AppUser>().WithOne().HasForeignKey<CandidateProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CandidateProfile>().Property(x => x.DisplayName).HasMaxLength(150);
        b.Entity<CandidateProfile>().Property(x => x.Headline).HasMaxLength(250);
        b.Entity<CandidateProfile>().Property(x => x.Summary).HasMaxLength(4000);
        b.Entity<CandidateProfile>().Property(x => x.Location).HasMaxLength(250);
        b.Entity<CandidateProfile>().Property(x => x.ContactEmail).HasMaxLength(254);
        b.Entity<CandidateProfile>().Property(x => x.ContactPhone).HasMaxLength(32);
        b.Entity<CandidateProfile>().Property(x => x.DesiredRole).HasMaxLength(150);
        b.Entity<CandidateProfile>().Property(x => x.PreferredWorkMode).HasMaxLength(24);
        b.Entity<CandidateProfile>().Property(x => x.Details).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        b.Entity<CandidateProfile>().Property(x => x.DetailsSchemaVersion).HasDefaultValue(1);
        b.Entity<CandidateProfile>().Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        b.Entity<CandidateProfile>().Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Entity<CandidateProfile>().Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        b.Entity<CandidateProfile>().ToTable(t => t.HasCheckConstraint("ck_candidate_profile", "(experience_years IS NULL OR experience_years >= 0) AND details_schema_version > 0 AND row_version >= 0"));
        b.Entity<CandidateProfileVisibility>().HasKey(x => new { x.CandidateId, x.FieldKey });
        b.Entity<CandidateProfileVisibility>().HasOne<CandidateProfile>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<CandidateProfileVisibility>().Property(x => x.FieldKey).HasMaxLength(64);
        b.Entity<CandidateProfileVisibility>().ToTable(t => t.HasCheckConstraint("ck_profile_visibility_field", "field_key IN ('display_name','headline','summary','location','contact_email','contact_phone','desired_role','preferred_work_mode','experience_years','skills','experience','education','projects','certifications')"));
        b.Entity<Company>().HasKey(x => x.Id);
        b.Entity<Company>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Company>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Company>().Property(x => x.Name).HasMaxLength(200);
        b.Entity<Company>().Property(x => x.Slug).HasMaxLength(220);
        b.Entity<Company>().Property(x => x.Description).HasMaxLength(4000);
        b.Entity<Company>().Property(x => x.WebsiteUrl).HasMaxLength(2048);
        b.Entity<Company>().Property(x => x.Location).HasMaxLength(250);
        b.Entity<Company>().Property(x => x.Industry).HasMaxLength(100);
        b.Entity<Company>().Property(x => x.SizeLabel).HasMaxLength(50);
        b.Entity<Company>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<Company>().ToTable(t => t.HasCheckConstraint("ck_company_row_version", "row_version >= 0"));
        b.Entity<JobPosting>().HasKey(x => x.Id);
        b.Entity<JobPosting>().HasAlternateKey(x => new { x.Id, x.CompanyId });
        b.Entity<JobPosting>().HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<JobPosting>().HasOne(x => x.CurrentVersion).WithMany().HasForeignKey(x => new { x.CurrentVersionId, x.Id }).HasPrincipalKey(x => new { x.Id, x.JobId }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<JobPosting>().Property(x => x.Status).HasMaxLength(24);
        b.Entity<JobPosting>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<JobPosting>().HasIndex(x => new { x.Status, x.CreatedAt });
        b.Entity<JobPosting>().HasIndex(x => new { x.CompanyId, x.Status, x.CreatedAt });
        b.Entity<JobPosting>().ToTable(t => t.HasCheckConstraint("ck_job_listing_state", "row_version >= 0 AND status IN ('draft','published','closed','archived') AND ((status = 'draft' AND closed_at IS NULL) OR (status = 'published' AND current_version_id IS NOT NULL AND published_at IS NOT NULL AND closed_at IS NULL) OR (status IN ('closed','archived') AND current_version_id IS NOT NULL AND published_at IS NOT NULL AND closed_at IS NOT NULL))"));
        b.Entity<JobVersion>().HasKey(x => x.Id);
        b.Entity<JobVersion>().HasAlternateKey(x => new { x.Id, x.JobId });
        b.Entity<JobVersion>().HasOne(x => x.Job).WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<JobVersion>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<JobVersion>().HasIndex(x => new { x.JobId, x.VersionNo }).IsUnique();
        b.Entity<JobVersion>().Property(x => x.Title).HasMaxLength(200);
        b.Entity<JobVersion>().Property(x => x.Industry).HasMaxLength(100);
        b.Entity<JobVersion>().Property(x => x.Location).HasMaxLength(250);
        b.Entity<JobVersion>().Property(x => x.EmploymentType).HasMaxLength(24);
        b.Entity<JobVersion>().Property(x => x.WorkMode).HasMaxLength(24);
        b.Entity<JobVersion>().Property(x => x.SalaryCurrency).HasColumnType("char(3)");
        b.Entity<JobVersion>().Property(x => x.SalaryPeriod).HasMaxLength(16);
        b.Entity<JobVersion>().Property(x => x.Language).HasMaxLength(12);
        b.Entity<JobVersion>().Property(x => x.ContentHash).HasColumnType("char(64)");
        b.Entity<JobVersion>().Property(x => x.ExperienceMin).HasPrecision(5, 2);
        b.Entity<JobVersion>().Property(x => x.ExperienceMax).HasPrecision(5, 2);
        b.Entity<JobVersion>().Property(x => x.SalaryMin).HasPrecision(18, 2);
        b.Entity<JobVersion>().Property(x => x.SalaryMax).HasPrecision(18, 2);
        b.Entity<JobVersion>().ToTable(t => t.HasCheckConstraint("ck_job_version_fields", "version_no > 0 AND employment_type IN ('full_time','part_time','contract','internship','temporary') AND work_mode IN ('onsite','hybrid','remote','unspecified') AND language IN ('vi','en','vi-en') AND (experience_min IS NULL OR experience_min >= 0) AND (experience_max IS NULL OR experience_max >= 0) AND (experience_min IS NULL OR experience_max IS NULL OR experience_min <= experience_max) AND (salary_min IS NULL OR salary_min >= 0) AND (salary_max IS NULL OR salary_max >= 0) AND (salary_min IS NULL OR salary_max IS NULL OR salary_min <= salary_max) AND ((salary_min IS NULL AND salary_max IS NULL AND salary_currency IS NULL AND salary_period IS NULL) OR ((salary_min IS NOT NULL OR salary_max IS NOT NULL) AND salary_currency ~ '^[A-Z]{3}$' AND salary_period IN ('hour','day','month','year','project'))) AND length(content_hash) = 64"));
        b.Entity<SavedJob>().HasKey(x => new { x.CandidateId, x.JobId });
        b.Entity<SavedJob>().HasOne<CandidateProfile>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SavedJob>().HasOne<JobPosting>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SavedJob>().HasIndex(x => new { x.JobId, x.CreatedAt });
        b.Entity<ApplicationRecord>().HasKey(x => x.Id);
        b.Entity<ApplicationRecord>().HasAlternateKey(x => new { x.Id, x.CandidateId });
        b.Entity<ApplicationRecord>().HasAlternateKey(x => new { x.Id, x.JobId });
        b.Entity<ApplicationRecord>().HasOne<CandidateProfile>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ApplicationRecord>().HasOne<JobPosting>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ApplicationRecord>().HasOne<ResumeVersion>().WithMany()
            .HasForeignKey(x => new { x.SubmittedResumeVersionId, x.CandidateId }).HasPrincipalKey(x => new { x.Id, x.CandidateId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ApplicationRecord>().HasOne<JobVersion>().WithMany()
            .HasForeignKey(x => new { x.SubmittedJobVersionId, x.JobId }).HasPrincipalKey(x => new { x.Id, x.JobId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ApplicationRecord>().Property(x => x.Status).HasMaxLength(24);
        b.Entity<ApplicationRecord>().Property(x => x.SubmissionHash).HasColumnType("char(64)");
        b.Entity<ApplicationRecord>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<ApplicationRecord>().HasIndex(x => new { x.CandidateId, x.SubmissionKey }).IsUnique();
        b.Entity<ApplicationRecord>().HasIndex(x => new { x.JobId, x.Status, x.SubmittedAt });
        b.Entity<ApplicationRecord>().HasIndex(x => new { x.CandidateId, x.JobId }).IsUnique()
            .HasFilter("status IN ('submitted','reviewing','shortlisted')");
        b.Entity<ApplicationRecord>().ToTable(t => t.HasCheckConstraint("ck_application_state",
            "status IN ('submitted','reviewing','shortlisted','rejected','withdrawn') AND row_version >= 0 AND length(submission_hash) = 64 AND ((status = 'withdrawn' AND withdrawn_at IS NOT NULL) OR (status <> 'withdrawn' AND withdrawn_at IS NULL))"));
        b.Entity<ApplicationStatusHistory>().HasKey(x => x.Id);
        b.Entity<ApplicationStatusHistory>().HasOne<ApplicationRecord>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ApplicationStatusHistory>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ApplicationStatusHistory>().Property(x => x.FromStatus).HasMaxLength(24);
        b.Entity<ApplicationStatusHistory>().Property(x => x.ToStatus).HasMaxLength(24);
        b.Entity<ApplicationStatusHistory>().Property(x => x.ReasonCode).HasMaxLength(80);
        b.Entity<ApplicationStatusHistory>().HasIndex(x => new { x.ApplicationId, x.ChangedAt, x.Id });
        b.Entity<ApplicationStatusHistory>().ToTable(t => t.HasCheckConstraint("ck_application_history_status",
            "(from_status IS NULL OR from_status IN ('submitted','reviewing','shortlisted','rejected','withdrawn')) AND to_status IN ('submitted','reviewing','shortlisted','rejected','withdrawn')"));
        b.Entity<ApplicationNoteRecord>().HasKey(x => x.Id);
        b.Entity<ApplicationNoteRecord>().HasOne<ApplicationRecord>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ApplicationNoteRecord>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ApplicationNoteRecord>().Property(x => x.Body).HasMaxLength(4000);
        b.Entity<ApplicationNoteRecord>().HasIndex(x => new { x.ApplicationId, x.CreatedAt, x.Id });
        b.Entity<ApplicationNoteRecord>().ToTable(t => t.HasCheckConstraint("ck_application_note_body", "length(body) BETWEEN 1 AND 4000"));
        b.Entity<JobCandidateBlockRecord>().HasKey(x => new { x.JobId, x.CandidateId });
        b.Entity<JobCandidateBlockRecord>().HasOne<JobPosting>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<JobCandidateBlockRecord>().HasOne<CandidateProfile>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<JobCandidateBlockRecord>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<JobCandidateBlockRecord>().HasIndex(x => new { x.CandidateId, x.CreatedAt });
        b.Entity<NotificationRecord>().HasKey(x => x.Id);
        b.Entity<NotificationRecord>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<NotificationRecord>().HasOne<ApplicationRecord>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<NotificationRecord>().HasOne<AiJob>().WithMany().HasForeignKey(x => x.AiJobId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<NotificationRecord>().Property(x => x.NotificationType).HasMaxLength(40);
        b.Entity<NotificationRecord>().Property(x => x.Title).HasMaxLength(200);
        b.Entity<NotificationRecord>().Property(x => x.DeduplicationKey).HasMaxLength(160);
        b.Entity<NotificationRecord>().HasIndex(x => new { x.RecipientUserId, x.DeduplicationKey }).IsUnique();
        b.Entity<NotificationRecord>().HasIndex(x => new { x.RecipientUserId, x.CreatedAt, x.Id })
            .HasDatabaseName("ix_notifications_recipient_created");
        b.Entity<NotificationRecord>().HasIndex(x => new { x.RecipientUserId, x.CreatedAt, x.Id }, "ix_notifications_unread")
            .HasFilter("read_at IS NULL");
        b.Entity<NotificationRecord>().ToTable(t => t.HasCheckConstraint("ck_notification_reference",
            "application_id IS NULL OR ai_job_id IS NULL"));
        b.Entity<StoredFile>().HasAlternateKey(x => new { x.Id, x.OwnerUserId });
        b.Entity<StoredFile>().HasIndex(x => new { x.Provider, x.ObjectKey }).IsUnique();
        b.Entity<StoredFile>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StoredFile>().ToTable(t => t.HasCheckConstraint("ck_file_ready", "size_bytes > 0 AND status IN ('pending','ready','failed','delete_pending') AND cleanup_attempt_count >= 0 AND (status <> 'ready' OR (sha256 IS NOT NULL AND verified_at IS NOT NULL))"));
        b.Entity<StoredFile>().Property(x => x.CleanupLastErrorCode).HasMaxLength(80);
        b.Entity<StoredFile>().HasIndex(x => new { x.Status, x.CreatedAt, x.CleanupNextAttemptAt, x.CleanupLeaseUntil })
            .HasDatabaseName("ix_stored_files_cleanup").HasFilter("status IN ('pending','delete_pending')");
        b.Entity<StoredFile>().Property(x => x.ObjectKey).HasMaxLength(64);
        b.Entity<StoredFile>().Property(x => x.OriginalName).HasMaxLength(255);
        b.Entity<Resume>().HasAlternateKey(x => new { x.Id, x.CandidateId });
        b.Entity<Resume>().HasOne<CandidateProfile>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Resume>().Property(x => x.Title).HasMaxLength(200);
        b.Entity<Resume>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<Resume>().Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        b.Entity<Resume>().ToTable(t => t.HasCheckConstraint("ck_resume_row_version", "row_version >= 0"));
        b.Entity<ResumeVersion>().HasOne<Resume>().WithMany().HasForeignKey(x => new { x.ResumeId, x.CandidateId }).HasPrincipalKey(x => new { x.Id, x.CandidateId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ResumeVersion>().HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.FileId, x.CandidateId }).HasPrincipalKey(x => new { x.Id, x.OwnerUserId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ResumeVersion>().HasAlternateKey(x => new { x.Id, x.CandidateId });
        b.Entity<ResumeVersion>().HasIndex(x => new { x.ResumeId, x.VersionNo }).IsUnique();
        b.Entity<ResumeVersion>().ToTable(t => t.HasCheckConstraint("ck_version", "version_no > 0 AND length(document_hash) = 64"));
        b.Entity<ResumeDataRevision>().HasAlternateKey(x => new { x.Id, x.ResumeVersionId });
        b.Entity<ResumeDataRevision>().HasOne<ResumeVersion>().WithMany()
            .HasForeignKey(x => new { x.ResumeVersionId, x.CandidateId }).HasPrincipalKey(x => new { x.Id, x.CandidateId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ResumeDataRevision>().HasOne<ResumeDataRevision>().WithMany()
            .HasForeignKey(x => new { x.ParentRevisionId, x.ResumeVersionId }).HasPrincipalKey(x => new { x.Id, x.ResumeVersionId }).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
        b.Entity<ResumeDataRevision>().HasOne<AiMethod>().WithMany().HasForeignKey(x => x.MethodVersionId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<ResumeDataRevision>().HasOne<AiJob>().WithMany().HasForeignKey(x => x.AiJobId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<ResumeDataRevision>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.ConfirmedBy).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<ResumeDataRevision>().HasIndex(x => new { x.ResumeVersionId, x.RevisionNo }).IsUnique();
        b.Entity<ResumeDataRevision>().HasIndex(x => x.AiJobId).IsUnique().HasFilter("ai_job_id IS NOT NULL");
        b.Entity<ResumeDataRevision>().HasIndex(x => new { x.CandidateId, x.ResumeVersionId, x.RevisionNo });
        b.Entity<ResumeDataRevision>().Property(x => x.Origin).HasMaxLength(24);
        b.Entity<ResumeDataRevision>().Property(x => x.DataHash).HasColumnType("char(64)");
        b.Entity<ResumeDataRevision>().Property(x => x.ParserVersion).HasMaxLength(80);
        b.Entity<ResumeDataRevision>().Property(x => x.StructuredData).HasColumnType("jsonb");
        b.Entity<ResumeDataRevision>().Property(x => x.SourceText).HasColumnType("jsonb");
        b.Entity<ResumeDataRevision>().Property(x => x.Evidence).HasColumnType("jsonb");
        b.Entity<ResumeDataRevision>().ToTable(t => t.HasCheckConstraint("ck_resume_data_revision",
            "revision_no > 0 AND schema_version > 0 AND length(data_hash) = 64 AND length(parser_version) BETWEEN 1 AND 80 AND " +
            "origin IN ('ai_extracted','user_confirmed','user_entered') AND parent_revision_id IS DISTINCT FROM id AND " +
            "((origin = 'ai_extracted' AND method_version_id IS NOT NULL AND ai_job_id IS NOT NULL AND confirmed_by IS NULL AND confirmed_at IS NULL) OR " +
            "(origin = 'user_confirmed' AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL) OR " +
            "(origin = 'user_entered' AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL))"));
        b.Entity<JobDataRevision>().HasAlternateKey(x => new { x.Id, x.JobVersionId });
        b.Entity<JobDataRevision>().HasOne<JobVersion>().WithMany().HasForeignKey(x => x.JobVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<JobDataRevision>().HasOne<JobDataRevision>().WithMany()
            .HasForeignKey(x => new { x.ParentRevisionId, x.JobVersionId }).HasPrincipalKey(x => new { x.Id, x.JobVersionId })
            .OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<JobDataRevision>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.ConfirmedBy)
            .OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<JobDataRevision>().HasIndex(x => new { x.JobVersionId, x.RevisionNo }).IsUnique();
        b.Entity<JobDataRevision>().HasIndex(x => new { x.JobVersionId, x.CreatedAt });
        b.Entity<JobDataRevision>().Property(x => x.Origin).HasMaxLength(24);
        b.Entity<JobDataRevision>().Property(x => x.DataHash).HasColumnType("char(64)");
        b.Entity<JobDataRevision>().Property(x => x.ParserVersion).HasMaxLength(80);
        b.Entity<JobDataRevision>().Property(x => x.StructuredData).HasColumnType("jsonb");
        b.Entity<JobDataRevision>().Property(x => x.SourceText).HasColumnType("jsonb");
        b.Entity<JobDataRevision>().Property(x => x.Evidence).HasColumnType("jsonb");
        b.Entity<JobDataRevision>().ToTable(t => t.HasCheckConstraint("ck_job_data_revision",
            "revision_no > 0 AND schema_version > 0 AND length(data_hash) = 64 AND length(parser_version) BETWEEN 1 AND 80 AND " +
            "origin IN ('local_extracted','user_confirmed') AND parent_revision_id IS DISTINCT FROM id AND " +
            "((origin = 'local_extracted' AND confirmed_by IS NULL AND confirmed_at IS NULL) OR " +
            "(origin = 'user_confirmed' AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL))"));
        b.Entity<CandidatePublicResume>().HasKey(x => x.CandidateId);
        b.Entity<CandidatePublicResume>().HasOne<CandidateProfile>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CandidatePublicResume>().HasOne<ResumeVersion>().WithMany()
            .HasForeignKey(x => new { x.ResumeVersionId, x.CandidateId }).HasPrincipalKey(x => new { x.Id, x.CandidateId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CandidatePublicResume>().HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.RedactedFileId, x.CandidateId })
            .HasPrincipalKey(x => new { x.Id, x.OwnerUserId }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<CandidatePublicResume>().Property(x => x.SourceHash).HasColumnType("char(64)");
        b.Entity<CandidatePublicResume>().Property(x => x.RedactionMethodVersion).HasMaxLength(80);
        b.Entity<CandidatePublicResume>().Property(x => x.Status).HasMaxLength(24);
        b.Entity<CandidatePublicResume>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<CandidatePublicResume>().HasIndex(x => new { x.Status, x.PublishedAt, x.CandidateId });
        b.Entity<CandidatePublicResume>().ToTable(t => t.HasCheckConstraint("ck_candidate_public_resume",
            "length(source_hash) = 64 AND row_version >= 0 AND status IN ('not_ready','ready') AND " +
            "((status = 'ready' AND redacted_file_id IS NOT NULL AND redaction_method_version IS NOT NULL AND redaction_verified_at IS NOT NULL) OR " +
            "(status = 'not_ready' AND redacted_file_id IS NULL AND redaction_method_version IS NULL AND redaction_verified_at IS NULL))"));
        b.Entity<AiMethod>().HasIndex(x => x.ConfigHash).IsUnique();
        b.Entity<AiMethod>().Property(x => x.Parameters).HasColumnType("jsonb");
        b.Entity<AiJob>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AiJob>().HasOne<ResumeVersion>().WithMany().HasForeignKey(x => new { x.ResumeVersionId, x.RequestedBy }).HasPrincipalKey(x => new { x.Id, x.CandidateId }).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        b.Entity<AiJob>().HasOne<AiMethod>().WithMany().HasForeignKey(x => x.MethodVersionId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AiJob>().HasIndex(x => new { x.RequestedBy, x.IdempotencyKey }).IsUnique();
        b.Entity<AiJob>().HasIndex(x => new { x.AvailableAt, x.CreatedAt }).HasFilter("status = 'queued'");
        b.Entity<AiJob>().HasIndex(x => x.LeaseUntil).HasFilter("status = 'running'");
        b.Entity<AiJob>().Property(x => x.OutputJson).HasColumnType("jsonb");
        b.Entity<AiJob>().ToTable(t => t.HasCheckConstraint("ck_job", "status IN ('queued','running','succeeded','failed','cancelled') AND attempt_count >= 0 AND max_attempts BETWEEN 1 AND 2 AND lease_generation >= 0"));
        b.Entity<BudgetPeriod>().HasIndex(x => new { x.Service, x.PeriodStart }).IsUnique();
        b.Entity<BudgetPeriod>().ToTable(t => t.HasCheckConstraint("ck_budget", "period_start < period_end AND limit_amount >= 0 AND spent_amount >= 0 AND reserved_amount >= 0"));
        b.Entity<BudgetReservation>().HasOne<AiJob>().WithMany().HasForeignKey(x => x.AiJobId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BudgetReservation>().HasOne<BudgetPeriod>().WithMany().HasForeignKey(x => x.BudgetPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<BudgetReservation>().HasIndex(x => x.AiJobId).IsUnique();
        b.Entity<BudgetReservation>().ToTable(t => t.HasCheckConstraint("ck_reservation", "unallocated_amount >= 0 AND reserved_remaining >= unallocated_amount AND per_call_estimate >= 0"));
        b.Entity<ExternalCallAttempt>().HasOne<AiJob>().WithMany().HasForeignKey(x => x.AiJobId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ExternalCallAttempt>().HasIndex(x => new { x.AiJobId, x.CallNo }).IsUnique();
        b.Entity<ExternalCallAttempt>().Property(x => x.PricingSnapshot).HasColumnType("jsonb");
        b.Entity<ExternalCallAttempt>().ToTable(t => t.HasCheckConstraint("ck_attempt", "call_no > 0 AND status IN ('prepared','in_flight','succeeded','failed','outcome_unknown') AND (actual_amount IS NULL OR actual_amount >= 0)"));
        b.Entity<CallBudgetAllocation>().HasKey(x => new { x.CallAttemptId, x.BudgetReservationId });
        b.Entity<CallBudgetAllocation>().HasOne<ExternalCallAttempt>().WithMany().HasForeignKey(x => x.CallAttemptId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CallBudgetAllocation>().HasOne<BudgetReservation>().WithMany().HasForeignKey(x => x.BudgetReservationId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CallBudgetAllocation>().ToTable(t => t.HasCheckConstraint("ck_allocation", "reserved_amount >= 0 AND status IN ('allocated','outcome_unknown','settled','released')"));

        b.Entity<AccountDeletionRequestRecord>().HasKey(x => x.Id);
        b.Entity<AccountDeletionRequestRecord>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AccountDeletionRequestRecord>().Property(x => x.Status).HasMaxLength(24);
        b.Entity<AccountDeletionRequestRecord>().Property(x => x.LeaseOwner).HasMaxLength(64);
        b.Entity<AccountDeletionRequestRecord>().Property(x => x.LastErrorCode).HasMaxLength(80);
        b.Entity<AccountDeletionRequestRecord>().HasIndex(x => new { x.UserId, x.RequestedAt });
        b.Entity<AccountDeletionRequestRecord>().HasIndex(x => new { x.PurgeAfter, x.RequestedAt, x.Id })
            .HasDatabaseName("ix_account_deletion_due")
            .HasFilter("status = 'pending'");
        b.Entity<AccountDeletionRequestRecord>().HasIndex(x => x.UserId).IsUnique().HasFilter("status = 'pending'")
            .HasDatabaseName("ux_account_deletion_one_pending");
        b.Entity<AccountDeletionRequestRecord>().ToTable(t => t.HasCheckConstraint("ck_account_deletion_request_state",
            "status IN ('pending','cancelled','purged') AND purge_after > requested_at AND attempt_count >= 0 AND lease_generation >= 0 AND " +
            "((status = 'cancelled' AND cancelled_at IS NOT NULL AND data_purged_at IS NULL AND purged_at IS NULL) OR " +
            "(status = 'pending' AND cancelled_at IS NULL AND purged_at IS NULL) OR " +
            "(status = 'purged' AND cancelled_at IS NULL AND data_purged_at IS NOT NULL AND purged_at IS NOT NULL))"));
        b.Entity<AccountDeletionObjectRecord>().HasKey(x => x.Id);
        b.Entity<AccountDeletionObjectRecord>().HasOne<AccountDeletionRequestRecord>().WithMany()
            .HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AccountDeletionObjectRecord>().Property(x => x.Provider).HasMaxLength(40);
        b.Entity<AccountDeletionObjectRecord>().Property(x => x.ObjectKey).HasMaxLength(64);
        b.Entity<AccountDeletionObjectRecord>().Property(x => x.Status).HasMaxLength(16);
        b.Entity<AccountDeletionObjectRecord>().Property(x => x.LastErrorCode).HasMaxLength(80);
        b.Entity<AccountDeletionObjectRecord>().HasIndex(x => new { x.RequestId, x.Provider, x.ObjectKey }).IsUnique();
        b.Entity<AccountDeletionObjectRecord>().HasIndex(x => new { x.RequestId, x.Status, x.CreatedAt });
        b.Entity<AccountDeletionObjectRecord>().ToTable(t => t.HasCheckConstraint("ck_account_deletion_object_state",
            "status IN ('pending','deleted') AND attempt_count >= 0 AND ((status = 'deleted' AND deleted_at IS NOT NULL) OR (status = 'pending' AND deleted_at IS NULL))"));

        // Keep framework Identity/DP names; use snake_case only for our feature tables.
        foreach (var type in b.Model.GetEntityTypes().Where(t => t.ClrType.Namespace == typeof(AppDb).Namespace && t.ClrType != typeof(AppUser)))
        {
            var table = type.ClrType.Name switch
            {
                nameof(AiMethod) => "ai_method_versions", nameof(AiJob) => "ai_jobs",
                nameof(AccountDeletionRequestRecord) => "account_deletion_requests",
                nameof(AccountDeletionObjectRecord) => "account_deletion_objects",
                nameof(Company) => "companies",
                nameof(CandidateProfileVisibility) => "candidate_profile_visibility",
                nameof(JobPosting) => "jobs", nameof(JobVersion) => "job_versions",
                nameof(JobDataRevision) => "job_data_revisions",
                nameof(SavedJob) => "saved_jobs",
                nameof(ApplicationRecord) => "applications",
                nameof(ApplicationStatusHistory) => "application_status_history",
                nameof(ApplicationNoteRecord) => "application_notes",
                nameof(JobCandidateBlockRecord) => "job_candidate_blocks",
                nameof(NotificationRecord) => "notifications",
                _ => Snake(type.ClrType.Name) + "s"
            };
            type.SetTableName(table);
            foreach (var p in type.GetProperties())
            {
                p.SetColumnName(Snake(p.Name));
                if (p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)) { p.SetPrecision(18); p.SetScale(8); }
            }
        }
        b.Entity<CandidateProfile>().Property(x => x.ExperienceYears).HasPrecision(5, 2);
        b.Entity<JobVersion>().Property(x => x.ExperienceMin).HasPrecision(5, 2);
        b.Entity<JobVersion>().Property(x => x.ExperienceMax).HasPrecision(5, 2);
        b.Entity<JobVersion>().Property(x => x.SalaryMin).HasPrecision(18, 2);
        b.Entity<JobVersion>().Property(x => x.SalaryMax).HasPrecision(18, 2);
    }
    private static string Snake(string name) => string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}

// Only the explicit EF CLI uses this factory. Application startup never migrates.
public sealed class AppDbFactory : IDesignTimeDbContextFactory<AppDb>
{
    public AppDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDb>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Database") ?? "Host=localhost;Port=55432;Database=careergap;Username=careergap", o => o.SetPostgresVersion(16, 0)).Options);
}
