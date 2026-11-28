using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerGap.Api.Applications;

public sealed record ApplyRequest(Guid ResumeVersionId, Guid SubmissionKey, string? CoverLetter = null);
public sealed record ApplicationDto(Guid Id, Guid JobId, string JobTitle, string CompanyName, string Status,
    DateTimeOffset SubmittedAt, DateTimeOffset StatusChangedAt, int ResumeVersionNo, long RowVersion,
    Guid CandidateId, bool CandidateBlocked, string? CoverLetter);
public sealed record ApplicationPage(IReadOnlyList<ApplicationDto> Items, int Page, int PageSize, int Total);
public sealed record ApplicationHistoryDto(string? FromStatus, string ToStatus, DateTimeOffset ChangedAt);
public sealed record ApplicationNoteDto(Guid Id, string Body, DateTimeOffset CreatedAt);
public sealed record NotificationDto(Guid Id, string NotificationType, Guid? ApplicationId, string Title, string Body,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, string DeduplicationKey, string? Href);

public static partial class ApplicationEndpoints
{
    private static readonly HashSet<string> ActiveStatuses = new(StringComparer.Ordinal) { "submitted", "reviewing", "shortlisted" };

    public static void MapApplicationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/jobs/{jobId:guid}/applications", SubmitAsync).RequireAuthorization();
        app.MapGet("/api/applications/mine", GetMineAsync).RequireAuthorization();
        app.MapGet("/api/jobs/{jobId:guid}/applications", GetForJobAsync).RequireAuthorization();
        app.MapGet("/api/applications/{id:guid}", GetOneAsync).RequireAuthorization();
        app.MapGet("/api/applications/{id:guid}/resume", DownloadSnapshotAsync).RequireAuthorization();
        app.MapPut("/api/applications/{id:guid}/status", ChangeStatusAsync).RequireAuthorization();
        app.MapPost("/api/applications/{id:guid}/withdraw", WithdrawAsync).RequireAuthorization();
        app.MapGet("/api/applications/{id:guid}/history", GetHistoryAsync).RequireAuthorization();
        app.MapGet("/api/applications/{id:guid}/notes", GetNotesAsync).RequireAuthorization();
        app.MapPost("/api/applications/{id:guid}/notes", AddNoteAsync).RequireAuthorization();
        app.MapPut("/api/jobs/{jobId:guid}/candidates/{candidateId:guid}/block", BlockCandidateAsync).RequireAuthorization();
        app.MapDelete("/api/jobs/{jobId:guid}/candidates/{candidateId:guid}/block", UnblockCandidateAsync).RequireAuthorization();
        app.MapGet("/api/notifications", GetNotificationsAsync).RequireAuthorization();
        app.MapGet("/api/notifications/inbox", GetNotificationInboxAsync).RequireAuthorization();
        app.MapPut("/api/notifications/read-all", MarkAllNotificationsReadAsync).RequireAuthorization();
        app.MapPut("/api/notifications/{id:guid}/read", MarkReadAsync).RequireAuthorization();
    }

    private static async Task<IResult> SubmitAsync(Guid jobId, ApplyRequest input, ClaimsPrincipal user, AppDb db,
        CancellationToken ct)
    {
        if (!TryUserId(user, out var candidateId)) return Results.Unauthorized();
        if (!user.IsInRole("Candidate")) return Results.Forbid();
        if (input.ResumeVersionId == Guid.Empty || input.SubmissionKey == Guid.Empty)
            return Results.Problem(statusCode: 400, title: "invalid_application");
        if (input.CoverLetter is { } rawLetter && (rawLetter.Length > 5000 || rawLetter.Contains('\0')))
            return Results.Problem(statusCode: 400, title: "invalid_cover_letter");
        var coverLetter = string.IsNullOrWhiteSpace(input.CoverLetter) ? null : input.CoverLetter.ReplaceLineEndings("\n").Trim();

        var inputHash = HashRequest(candidateId, jobId, input.ResumeVersionId, coverLetter);
        var existing = await db.Applications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CandidateId == candidateId && x.SubmissionKey == input.SubmissionKey, ct);
        if (existing is not null)
            return existing.SubmissionHash == inputHash
                ? Results.Ok(await QueryApplications(db, applicationId: existing.Id).SingleAsync(ct))
                : Results.Problem(statusCode: 409, title: "idempotency_conflict");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id = {jobId} FOR UPDATE", ct);
        var job = await db.Jobs.AsNoTracking().Include(x => x.Company).Include(x => x.CurrentVersion)
            .SingleOrDefaultAsync(x => x.Id == jobId && x.Company!.ArchivedAt == null, ct);
        if (job is null || !await db.Users.AsNoTracking().AnyAsync(u => u.Id == job.Company!.OwnerUserId && u.AccountStatus == "active", ct))
            return Results.NotFound();

        existing = await db.Applications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.CandidateId == candidateId && x.SubmissionKey == input.SubmissionKey, ct);
        if (existing is not null)
        {
            if (existing.SubmissionHash != inputHash) return Results.Problem(statusCode: 409, title: "idempotency_conflict");
            return Results.Ok(await QueryApplications(db, applicationId: existing.Id).SingleAsync(ct));
        }

        var now = DateTimeOffset.UtcNow;
        if (!await CareerGap.Api.Recruitment.BusinessIdentityEndpoints.Verified(db,job.Company!.OwnerUserId,job.CompanyId,ct)) return Results.Problem(statusCode:409,title:"business_verification_required");
        if (job.Status != "published" || job.CurrentVersionId is null || job.CurrentVersion is null ||
            (job.ApplicationDeadline is { } deadline && deadline <= now))
            return Results.Problem(statusCode: 409, title: "job_not_accepting_applications");
        if (job.MaxApplications is { } maxApplications)
        {
            var activeCount = await db.Applications.CountAsync(x => x.JobId == jobId && ActiveStatuses.Contains(x.Status), ct);
            if (activeCount >= maxApplications)
                return Results.Problem(statusCode: 409, title: "job_capacity_reached");
        }
        if (await db.JobCandidateBlocks.AsNoTracking().AnyAsync(x => x.JobId == jobId && x.CandidateId == candidateId, ct))
            return Results.Problem(statusCode: 409, title: "candidate_blocked");
        var resumeVersion = await db.ResumeVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == input.ResumeVersionId && x.CandidateId == candidateId, ct);
        if (resumeVersion is null) return Results.NotFound();
        var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == resumeVersion.FileId &&
            x.OwnerUserId == candidateId && x.Status == "ready" && x.Sha256 == resumeVersion.DocumentHash && x.VerifiedAt != null, ct);
        if (file is null) return Results.NotFound();
        if (await db.Applications.AnyAsync(x => x.CandidateId == candidateId && x.JobId == jobId && ActiveStatuses.Contains(x.Status), ct))
            return Results.Problem(statusCode: 409, title: "active_application_exists");

        var application = new ApplicationRecord
        {
            CandidateId = candidateId,
            JobId = jobId,
            SubmittedResumeVersionId = resumeVersion.Id,
            SubmittedJobVersionId = job.CurrentVersionId.Value,
            Status = "submitted",
            SubmittedAt = now,
            StatusChangedAt = now,
            SubmissionKey = input.SubmissionKey,
            SubmissionHash = inputHash,
            CoverLetter = coverLetter
        };
        db.Applications.Add(application);
        db.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            ApplicationId = application.Id,
            FromStatus = null,
            ToStatus = "submitted",
            ActorUserId = candidateId,
            ChangedAt = now
        });
        if (await db.IsNotifyRecruitmentEnabledAsync(candidateId, ct))
        {
            db.Notifications.Add(new NotificationRecord
            {
                RecipientUserId = candidateId,
                NotificationType = "application_submitted",
                ApplicationId = application.Id,
                Title = "Đã nộp hồ sơ",
                Body = $"Đơn ứng tuyển cho vị trí {job.CurrentVersion.Title} đã được ghi nhận.",
                CreatedAt = now,
                DeduplicationKey = $"application:{application.Id}:submitted:candidate"
            });
        }
        if (await db.IsNotifyRecruitmentEnabledAsync(job.Company!.OwnerUserId, ct))
        {
            db.Notifications.Add(new NotificationRecord
            {
                RecipientUserId = job.Company!.OwnerUserId,
                NotificationType = "application_submitted",
                ApplicationId = application.Id,
                Title = "Có hồ sơ ứng tuyển mới",
                Body = $"Có ứng viên nộp hồ sơ vào vị trí {job.CurrentVersion.Title}.",
                CreatedAt = now,
                DeduplicationKey = $"application:{application.Id}:submitted:recruiter"
            });
        }
        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            await transaction.RollbackAsync(ct);
            return Results.Problem(statusCode: 409, title: "application_conflict");
        }
        var result = await QueryApplications(db, applicationId: application.Id).SingleAsync(ct);
        return Results.Created($"/api/applications/{application.Id}", result);
    }

    private static async Task<IResult> GetMineAsync(ClaimsPrincipal user, int? page, int? pageSize, Guid? jobId, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var candidateId)) return Results.Unauthorized();
        if (!user.IsInRole("Candidate")) return Results.Forbid();
        if (!ValidPage(page, pageSize, out var currentPage, out var size)) return Results.Problem(statusCode: 400, title: "invalid_pagination");
        var retentionCutoff = DateTimeOffset.UtcNow.AddDays(-90);
        var query = db.Applications.AsNoTracking().Where(x => x.CandidateId == candidateId &&
            (ActiveStatuses.Contains(x.Status) || ((x.Status == "rejected" || x.Status == "withdrawn") && x.StatusChangedAt > retentionCutoff)));
        if (jobId is not null) query = query.Where(x => x.JobId == jobId);
        return Results.Ok(await PageApplicationsAsync(db, query, currentPage, size, ct));
    }

    private static async Task<IResult> GetForJobAsync(Guid jobId, ClaimsPrincipal user, int? page, int? pageSize, string? status,
        AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var recruiterId)) return Results.Unauthorized();
        if (!user.IsInRole("Recruiter")) return Results.Forbid();
        if (!ValidPage(page, pageSize, out var currentPage, out var size)) return Results.Problem(statusCode: 400, title: "invalid_pagination");
        if (!await OwnsJobAsync(db, jobId, recruiterId, ct)) return Results.NotFound();
        if (status is not null && status is not ("submitted" or "reviewing" or "shortlisted" or "rejected" or "withdrawn"))
            return Results.Problem(statusCode: 400, title: "invalid_application_status_filter");
        var retentionCutoff = DateTimeOffset.UtcNow.AddDays(-90);
        var query = db.Applications.AsNoTracking().Where(x => x.JobId == jobId &&
            db.Users.Any(account => account.Id == x.CandidateId && account.AccountStatus == "active") &&
            (ActiveStatuses.Contains(x.Status) ||
             ((x.Status == "rejected" || x.Status == "withdrawn") && x.StatusChangedAt > retentionCutoff)));
        if (status is not null) query = query.Where(x => x.Status == status);
        return Results.Ok(await PageApplicationsAsync(db, query, currentPage, size, ct, includeBlockState: true));
    }

    private static async Task<IResult> GetOneAsync(Guid id, ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var userId)) return Results.Unauthorized();
        var retentionCutoff = DateTimeOffset.UtcNow.AddDays(-90);
        var recruiter = user.IsInRole("Recruiter");
        var application = await db.Applications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id && ((x.CandidateId == userId &&
                (ActiveStatuses.Contains(x.Status) || ((x.Status == "rejected" || x.Status == "withdrawn") && x.StatusChangedAt > retentionCutoff))) ||
                (recruiter && db.Users.Any(account => account.Id == x.CandidateId && account.AccountStatus == "active") &&
                 db.Jobs.Any(j => j.Id == x.JobId && j.Company!.OwnerUserId == userId && db.BusinessIdentities.Any(b => b.UserId == userId && b.CompanyId == j.CompanyId)) &&
                 (ActiveStatuses.Contains(x.Status) ||
                  ((x.Status == "rejected" || x.Status == "withdrawn") && x.StatusChangedAt > retentionCutoff)))), ct);
        if (application is null) return Results.NotFound();
        return Results.Ok(await QueryApplications(db, applicationId: id, includeBlockState: recruiter).SingleAsync(ct));
    }

    private static async Task<IResult> DownloadSnapshotAsync(Guid id, ClaimsPrincipal user, AppDb db,
        IFileStorage storage, CancellationToken ct)
    {
        if (!TryUserId(user, out var userId)) return Results.Unauthorized();
        var snapshot = await (from application in db.Applications.AsNoTracking()
                              join job in db.Jobs.AsNoTracking() on application.JobId equals job.Id
                              join company in db.Companies.AsNoTracking() on job.CompanyId equals company.Id
                              join version in db.ResumeVersions.AsNoTracking() on application.SubmittedResumeVersionId equals version.Id
                              where application.Id == id
                              select new { Application = application, OwnerUserId = company.OwnerUserId, job.CompanyId, version.FileId,
                                  CandidateActive = db.Users.Any(account => account.Id == application.CandidateId && account.AccountStatus == "active"),
                                  HasBusinessIdentity = db.BusinessIdentities.Any(b => b.UserId == userId && b.CompanyId == job.CompanyId) }).SingleOrDefaultAsync(ct);
        if (snapshot is null) return Results.NotFound();
        var withinRetention = ActiveStatuses.Contains(snapshot.Application.Status) ||
            (snapshot.Application.Status is "rejected" or "withdrawn" && snapshot.Application.StatusChangedAt.AddDays(90) > DateTimeOffset.UtcNow);
        var candidateAccess = snapshot.CandidateActive && user.IsInRole("Candidate") && snapshot.Application.CandidateId == userId && withinRetention;
        var recruiterAccess = snapshot.CandidateActive && user.IsInRole("Recruiter") && snapshot.OwnerUserId == userId && snapshot.HasBusinessIdentity &&
            (ActiveStatuses.Contains(snapshot.Application.Status) ||
             (snapshot.Application.Status is "rejected" or "withdrawn" && snapshot.Application.StatusChangedAt.AddDays(90) > DateTimeOffset.UtcNow));
        if (!candidateAccess && !recruiterAccess) return Results.NotFound();
        var file = await db.StoredFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == snapshot.FileId &&
            x.OwnerUserId == snapshot.Application.CandidateId && x.Status == "ready", ct);
        if (file is null) return Results.NotFound();
        return await FileEndpoints.ServeFileAsync(file, storage, ct);
    }

    private static async Task<ApplicationPage> PageApplicationsAsync(AppDb db, IQueryable<ApplicationRecord> query,
        int page, int pageSize, CancellationToken ct, bool includeBlockState = false)
    {
        var total = await query.CountAsync(ct);
        var ids = await query.OrderByDescending(x => x.SubmittedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Id).ToArrayAsync(ct);
        var itemsById = await QueryApplications(db, applicationIds: ids, includeBlockState: includeBlockState).ToDictionaryAsync(x => x.Id, ct);
        var items = ids.Select(id => itemsById[id]).ToArray();
        return new ApplicationPage(items, page, pageSize, total);
    }

    private static IQueryable<ApplicationDto> QueryApplications(AppDb db, Guid? applicationId = null,
        Guid[]? applicationIds = null, bool includeBlockState = false)
    {
        var applications = db.Applications.AsNoTracking().AsQueryable();
        if (applicationId is { } id) applications = applications.Where(x => x.Id == id);
        if (applicationIds is not null) applications = applications.Where(x => applicationIds.Contains(x.Id));
        return from application in applications
               join resumeVersion in db.ResumeVersions.AsNoTracking() on application.SubmittedResumeVersionId equals resumeVersion.Id
               join jobVersion in db.JobVersions.AsNoTracking() on application.SubmittedJobVersionId equals jobVersion.Id
               join jobPosting in db.Jobs.AsNoTracking() on application.JobId equals jobPosting.Id
               join company in db.Companies.AsNoTracking() on jobPosting.CompanyId equals company.Id
               select new ApplicationDto(application.Id, application.JobId, jobVersion.Title, company.Name, application.Status,
                   application.SubmittedAt, application.StatusChangedAt, resumeVersion.VersionNo, application.RowVersion,
                   application.CandidateId, includeBlockState && db.JobCandidateBlocks.Any(block =>
                       block.JobId == application.JobId && block.CandidateId == application.CandidateId), application.CoverLetter);
    }

    private static string HashRequest(Guid candidateId, Guid jobId, Guid resumeVersionId, string? coverLetter) =>
        // Keep hashes of historical submissions without a letter compatible with retries.
        Convert.ToHexStringLower(SHA256.HashData(coverLetter is null
            ? JsonSerializer.SerializeToUtf8Bytes(new { candidateId, jobId, resumeVersionId })
            : JsonSerializer.SerializeToUtf8Bytes(new { candidateId, jobId, resumeVersionId, coverLetter })));

    private static bool ValidPage(int? page, int? pageSize, out int currentPage, out int size)
    {
        currentPage = page ?? 1;
        size = pageSize ?? 20;
        return currentPage is >= 1 and <= 1_000_000 && size is >= 1 and <= 50;
    }

    private static bool TryUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
