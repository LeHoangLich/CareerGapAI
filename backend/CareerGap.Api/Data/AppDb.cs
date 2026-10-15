using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CareerGap.Api.Data;

public sealed class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<CandidateProfile> CandidateProfiles => Set<CandidateProfile>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<ResumeVersion> ResumeVersions => Set<ResumeVersion>();
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
        b.Entity<StoredFile>().HasAlternateKey(x => new { x.Id, x.OwnerUserId });
        b.Entity<StoredFile>().HasIndex(x => new { x.Provider, x.ObjectKey }).IsUnique();
        b.Entity<StoredFile>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<StoredFile>().ToTable(t => t.HasCheckConstraint("ck_file_ready", "size_bytes > 0 AND status IN ('pending','ready','failed') AND (status <> 'ready' OR (sha256 IS NOT NULL AND verified_at IS NOT NULL))"));
        b.Entity<StoredFile>().Property(x => x.ObjectKey).HasMaxLength(64);
        b.Entity<StoredFile>().Property(x => x.OriginalName).HasMaxLength(255);
        b.Entity<Resume>().HasAlternateKey(x => new { x.Id, x.CandidateId });
        b.Entity<Resume>().HasOne<CandidateProfile>().WithMany().HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Resume>().Property(x => x.Title).HasMaxLength(200);
        b.Entity<ResumeVersion>().HasOne<Resume>().WithMany().HasForeignKey(x => new { x.ResumeId, x.CandidateId }).HasPrincipalKey(x => new { x.Id, x.CandidateId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ResumeVersion>().HasOne<StoredFile>().WithMany().HasForeignKey(x => new { x.FileId, x.CandidateId }).HasPrincipalKey(x => new { x.Id, x.OwnerUserId }).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ResumeVersion>().HasAlternateKey(x => new { x.Id, x.CandidateId });
        b.Entity<ResumeVersion>().HasIndex(x => new { x.ResumeId, x.VersionNo }).IsUnique();
        b.Entity<ResumeVersion>().ToTable(t => t.HasCheckConstraint("ck_version", "version_no > 0 AND length(document_hash) = 64"));
        b.Entity<AiMethod>().HasIndex(x => x.ConfigHash).IsUnique();
        b.Entity<AiMethod>().Property(x => x.Parameters).HasColumnType("jsonb");
        b.Entity<AiJob>().HasOne<AppUser>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        b.Entity<AiJob>().HasOne<ResumeVersion>().WithMany().HasForeignKey(x => new { x.ResumeVersionId, x.RequestedBy }).HasPrincipalKey(x => new { x.Id, x.CandidateId }).OnDelete(DeleteBehavior.Restrict);
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

        // Keep framework Identity/DP names; use snake_case only for our feature tables.
        foreach (var type in b.Model.GetEntityTypes().Where(t => t.ClrType.Namespace == typeof(AppDb).Namespace && t.ClrType != typeof(AppUser)))
        {
            var table = type.ClrType.Name switch
            {
                nameof(AiMethod) => "ai_method_versions", nameof(AiJob) => "ai_jobs",
                _ => Snake(type.ClrType.Name) + "s"
            };
            type.SetTableName(table);
            foreach (var p in type.GetProperties())
            {
                p.SetColumnName(Snake(p.Name));
                if (p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)) { p.SetPrecision(18); p.SetScale(8); }
            }
        }
    }
    private static string Snake(string name) => string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}

// Only the explicit EF CLI uses this factory. Application startup never migrates.
public sealed class AppDbFactory : IDesignTimeDbContextFactory<AppDb>
{
    public AppDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AppDb>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__Database") ?? "Host=localhost;Port=55432;Database=careergap;Username=careergap", o => o.SetPostgresVersion(16, 0)).Options);
}
