using System.Security.Claims;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Applications;

public sealed record ApplicationStatusRequest(long RowVersion, string? Status);
public sealed record ApplicationWithdrawRequest(long RowVersion);
public sealed record ApplicationNoteRequest(string? Body);

public static partial class ApplicationEndpoints
{
    private static readonly HashSet<string> KnownStatuses = new(StringComparer.Ordinal)
        { "submitted", "reviewing", "shortlisted", "rejected", "withdrawn" };
    private static readonly IReadOnlyDictionary<string, HashSet<string>> RecruiterTransitions =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["submitted"] = new(StringComparer.Ordinal) { "reviewing", "shortlisted", "rejected" },
            ["reviewing"] = new(StringComparer.Ordinal) { "shortlisted", "rejected" },
            ["shortlisted"] = new(StringComparer.Ordinal) { "reviewing", "rejected" }
        };

    private static async Task<IResult> ChangeStatusAsync(Guid id, ApplicationStatusRequest input,
        ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var recruiterId)) return Results.Unauthorized();
        if (!user.IsInRole("Recruiter")) return Results.Forbid();
        var nextStatus = input.Status?.Trim();
        if (nextStatus is null || !KnownStatuses.Contains(nextStatus) || nextStatus == "withdrawn")
            return Results.BadRequest(new { title = "invalid_application_status" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM applications WHERE id = {id} FOR UPDATE", ct);
        var application = await db.Applications.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (application is null) return Results.NotFound();
        if (!await OwnsJobAsync(db, application.JobId, recruiterId, ct) ||
            !await CandidateAccountActiveAsync(db, application.CandidateId, ct) ||
            !RecruiterCanRead(application, DateTimeOffset.UtcNow)) return Results.NotFound();
        if (application.RowVersion != input.RowVersion) return Results.Conflict(new { title = "application_conflict" });
        if (!RecruiterTransitions.TryGetValue(application.Status, out var allowed) || !allowed.Contains(nextStatus))
            return Results.Conflict(new { title = "invalid_status_transition" });

        var previousStatus = application.Status;
        var now = DateTimeOffset.UtcNow;
        application.Status = nextStatus;
        application.StatusChangedAt = now;
        application.RowVersion++;
        db.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            ApplicationId = application.Id,
            FromStatus = previousStatus,
            ToStatus = nextStatus,
            ActorUserId = recruiterId,
            ChangedAt = now
        });
        db.Notifications.Add(new NotificationRecord
        {
            RecipientUserId = application.CandidateId,
            NotificationType = "application_status_changed",
            ApplicationId = application.Id,
            Title = "Hồ sơ được cập nhật",
            Body = $"Trạng thái hồ sơ ứng tuyển đã chuyển thành {StatusLabel(nextStatus)}.",
            CreatedAt = now,
            DeduplicationKey = $"application:{application.Id}:status:{application.RowVersion}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(await QueryApplications(db, applicationId: id, includeBlockState: true).SingleAsync(ct));
    }

    private static async Task<IResult> WithdrawAsync(Guid id, ApplicationWithdrawRequest input,
        ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var candidateId)) return Results.Unauthorized();
        if (!user.IsInRole("Candidate")) return Results.Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM applications WHERE id = {id} FOR UPDATE", ct);
        var application = await db.Applications.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (application is null || application.CandidateId != candidateId || !CandidateCanRead(application, DateTimeOffset.UtcNow)) return Results.NotFound();
        if (application.RowVersion != input.RowVersion) return Results.Conflict(new { title = "application_conflict" });
        if (!ActiveStatuses.Contains(application.Status)) return Results.Conflict(new { title = "application_is_terminal" });

        var previousStatus = application.Status;
        var now = DateTimeOffset.UtcNow;
        var recruiterId = await db.Jobs.Where(x => x.Id == application.JobId)
            .Select(x => x.Company!.OwnerUserId).SingleAsync(ct);
        application.Status = "withdrawn";
        application.StatusChangedAt = now;
        application.WithdrawnAt = now;
        application.RowVersion++;
        db.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            ApplicationId = application.Id,
            FromStatus = previousStatus,
            ToStatus = "withdrawn",
            ActorUserId = candidateId,
            ChangedAt = now
        });
        var jobTitle = await db.JobVersions.Where(x => x.Id == application.SubmittedJobVersionId)
            .Select(x => x.Title).SingleAsync(ct);
        db.Notifications.Add(new NotificationRecord
        {
            RecipientUserId = recruiterId,
            NotificationType = "application_status_changed",
            ApplicationId = application.Id,
            Title = "Ứng viên đã rút hồ sơ",
            Body = $"Ứng viên đã rút hồ sơ cho vị trí {jobTitle}.",
            CreatedAt = now,
            DeduplicationKey = $"application:{application.Id}:status:{application.RowVersion}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok(await QueryApplications(db, applicationId: id).SingleAsync(ct));
    }

    private static async Task<IResult> GetHistoryAsync(Guid id, ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var userId)) return Results.Unauthorized();
        var application = await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (application is null || !await CanReadAsParticipantAsync(application, user, userId, db, ct)) return Results.NotFound();
        var history = await db.ApplicationStatusHistory.AsNoTracking().Where(x => x.ApplicationId == id)
            .OrderBy(x => x.ChangedAt).ThenBy(x => x.Id)
            .Select(x => new ApplicationHistoryDto(x.FromStatus, x.ToStatus, x.ChangedAt)).ToListAsync(ct);
        return Results.Ok(history);
    }

    private static async Task<IResult> GetNotesAsync(Guid id, ClaimsPrincipal user, int? page, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var recruiterId)) return Results.Unauthorized();
        if (!user.IsInRole("Recruiter")) return Results.Forbid();
        var currentPage = page ?? 1;
        if (currentPage is < 1 or > 1_000_000) return Results.BadRequest();
        var application = await db.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (application is null || !await OwnsJobAsync(db, application.JobId, recruiterId, ct) ||
            !await CandidateAccountActiveAsync(db, application.CandidateId, ct) ||
            !RecruiterCanRead(application, DateTimeOffset.UtcNow)) return Results.NotFound();
        var notes = await db.ApplicationNotes.AsNoTracking().Where(x => x.ApplicationId == id)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((currentPage - 1) * 20).Take(20)
            .Select(x => new ApplicationNoteDto(x.Id, x.Body, x.CreatedAt)).ToListAsync(ct);
        return Results.Ok(notes);
    }

    private static async Task<IResult> AddNoteAsync(Guid id, ApplicationNoteRequest input,
        ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var recruiterId)) return Results.Unauthorized();
        if (!user.IsInRole("Recruiter")) return Results.Forbid();
        var body = input.Body?.Trim();
        if (string.IsNullOrEmpty(body) || body.Length > 4000)
            return Results.BadRequest(new { title = "invalid_application_note" });

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM applications WHERE id = {id} FOR UPDATE", ct);
        var application = await db.Applications.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (application is null || !await OwnsJobAsync(db, application.JobId, recruiterId, ct) ||
            !await CandidateAccountActiveAsync(db, application.CandidateId, ct) ||
            !RecruiterCanRead(application, DateTimeOffset.UtcNow)) return Results.NotFound();
        var note = new ApplicationNoteRecord
        {
            ApplicationId = id,
            AuthorUserId = recruiterId,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ApplicationNotes.Add(note);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/applications/{id}/notes", new ApplicationNoteDto(note.Id, note.Body, note.CreatedAt));
    }

    private static async Task<IResult> BlockCandidateAsync(Guid jobId, Guid candidateId,
        ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var recruiterId)) return Results.Unauthorized();
        if (!user.IsInRole("Recruiter")) return Results.Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id = {jobId} FOR UPDATE", ct);
        var jobExists = await db.Jobs.AnyAsync(x => x.Id == jobId, ct);
        if (!jobExists || !await OwnsJobAsync(db, jobId, recruiterId, ct)) return Results.NotFound();
        if (!await db.CandidateProfiles.AnyAsync(x => x.UserId == candidateId, ct)) return Results.NotFound();
        var existing = await db.JobCandidateBlocks.FindAsync([jobId, candidateId], ct);
        if (existing is null)
        {
            db.JobCandidateBlocks.Add(new JobCandidateBlockRecord
            {
                JobId = jobId,
                CandidateId = candidateId,
                CreatedByUserId = recruiterId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UnblockCandidateAsync(Guid jobId, Guid candidateId,
        ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var recruiterId)) return Results.Unauthorized();
        if (!user.IsInRole("Recruiter")) return Results.Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id = {jobId} FOR UPDATE", ct);
        var jobExists = await db.Jobs.AnyAsync(x => x.Id == jobId, ct);
        if (!jobExists || !await OwnsJobAsync(db, jobId, recruiterId, ct)) return Results.NotFound();
        var existing = await db.JobCandidateBlocks.FindAsync([jobId, candidateId], ct);
        if (existing is not null)
        {
            db.JobCandidateBlocks.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<bool> CanReadAsParticipantAsync(ApplicationRecord application, ClaimsPrincipal user,
        Guid userId, AppDb db, CancellationToken ct)
    {
        if (user.IsInRole("Candidate")) return application.CandidateId == userId && CandidateCanRead(application, DateTimeOffset.UtcNow);
        return user.IsInRole("Recruiter") && await OwnsJobAsync(db, application.JobId, userId, ct) &&
            await CandidateAccountActiveAsync(db, application.CandidateId, ct) &&
            RecruiterCanRead(application, DateTimeOffset.UtcNow);
    }

    private static bool CandidateCanRead(ApplicationRecord application, DateTimeOffset now) =>
        ActiveStatuses.Contains(application.Status) ||
        ((application.Status is "rejected" or "withdrawn") && application.StatusChangedAt.AddDays(90) > now);

    private static Task<bool> CandidateAccountActiveAsync(AppDb db, Guid candidateId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(x => x.Id == candidateId && x.AccountStatus == "active", ct);

    private static async Task<bool> OwnsJobAsync(AppDb db, Guid jobId, Guid recruiterId, CancellationToken ct) =>
        await db.Jobs.AsNoTracking().AnyAsync(x => x.Id == jobId && x.Company!.OwnerUserId == recruiterId, ct);

    private static bool RecruiterCanRead(ApplicationRecord application, DateTimeOffset now) =>
        ActiveStatuses.Contains(application.Status) ||
        ((application.Status is "rejected" or "withdrawn") && application.StatusChangedAt > now.AddDays(-90));

    private static string StatusLabel(string status) => status switch
    {
        "submitted" => "đã nộp",
        "reviewing" => "đang xem xét",
        "shortlisted" => "được chọn vào danh sách tiếp theo",
        "rejected" => "không được tiếp tục trong quy trình này",
        "withdrawn" => "đã rút",
        _ => "được cập nhật"
    };
}
