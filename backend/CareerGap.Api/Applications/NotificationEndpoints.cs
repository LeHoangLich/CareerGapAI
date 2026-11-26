using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Applications;

public sealed record RecruitmentNotificationSignal(int Total,int Unread,string Revision);

public sealed record NotificationInboxPage(IReadOnlyList<NotificationDto> Items, int Page, int PageSize, int Total, int Unread);

public static partial class ApplicationEndpoints
{
    private static IQueryable<NotificationRecord> VisibleNotifications(AppDb db, Guid userId, bool recruiter)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-90);
        return db.Notifications.Where(n => n.RecipientUserId == userId &&
            (n.AiJobId == null || db.AiJobs.Any(job => job.Id == n.AiJobId && job.RequestedBy == userId)) &&
            (n.ApplicationId == null || db.Applications.Any(a => a.Id == n.ApplicationId &&
                (a.CandidateId == userId || (recruiter &&
                    db.Users.Any(candidate => candidate.Id == a.CandidateId && candidate.AccountStatus == "active") &&
                    db.Jobs.Any(job => job.Id == a.JobId && job.Company!.OwnerUserId == userId))) &&
                (ActiveStatuses.Contains(a.Status) ||
                    ((a.Status == "rejected" || a.Status == "withdrawn") && a.StatusChangedAt > cutoff)))));
    }

    internal static async Task<RecruitmentNotificationSignal> NotificationSignal(AppDb db,Guid userId,bool recruiter,CancellationToken ct)
    {
        // Only minimal owned metadata leaves SQL; SSE receives an opaque digest, never IDs or content.
        var rows=await VisibleNotifications(db,userId,recruiter).AsNoTracking().OrderBy(n=>n.Id).Select(n=>new{n.Id,n.ReadAt}).ToArrayAsync(ct);
        var stamp=string.Join(";",rows.Select(n=>n.Id.ToString("N")+":"+(n.ReadAt?.UtcTicks.ToString(CultureInfo.InvariantCulture)??"unread")));
        return new(rows.Length,rows.Count(n=>n.ReadAt==null),Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(stamp))));
    }

    private static NotificationDto ToNotificationDto(NotificationRecord n) => new(n.Id, n.NotificationType, n.ApplicationId,
        n.Title, n.Body, n.CreatedAt, n.ReadAt, n.DeduplicationKey,
        n.ApplicationId is { } id ? $"#application?id={id}" : null);

    // Keep the array response for existing clients; the new inbox has paging metadata.
    private static async Task<IResult> GetNotificationsAsync(ClaimsPrincipal user, int? page, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var userId)) return Results.Unauthorized();
        var currentPage = page ?? 1;
        if (currentPage is < 1 or > 10000) return Results.Problem(statusCode: 400, title: "invalid_pagination");
        var records = await VisibleNotifications(db, userId, user.IsInRole("Recruiter")).AsNoTracking()
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Skip((currentPage - 1) * 20).Take(20).ToListAsync(ct);
        return Results.Ok(records.Select(ToNotificationDto).ToArray());
    }

    private static async Task<IResult> GetNotificationInboxAsync(ClaimsPrincipal user, int? page, int? pageSize,
        string? read, string? type, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var userId)) return Results.Unauthorized();
        if (!ValidPage(page, pageSize, out var currentPage, out var size) ||
            read is not (null or "all" or "read" or "unread") ||
            type is not (null or "application_submitted" or "application_status_changed" or "system_notice"))
            return Results.Problem(statusCode: 400, title: "invalid_notification_filter");
        var visible = VisibleNotifications(db, userId, user.IsInRole("Recruiter")).AsNoTracking();
        var unread = await visible.CountAsync(n => n.ReadAt == null, ct);
        var query = visible;
        if (read == "read") query = query.Where(n => n.ReadAt != null);
        if (read == "unread") query = query.Where(n => n.ReadAt == null);
        if (type is not null) query = query.Where(n => n.NotificationType == type);
        var total = await query.CountAsync(ct);
        var records = await query.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id)
            .Skip((currentPage - 1) * size).Take(size).ToListAsync(ct);
        return Results.Ok(new NotificationInboxPage(records.Select(ToNotificationDto).ToArray(), currentPage, size, total, unread));
    }

    private static async Task<IResult> MarkReadAsync(Guid id, ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var userId)) return Results.Unauthorized();
        var notification = await VisibleNotifications(db, userId, user.IsInRole("Recruiter")).SingleOrDefaultAsync(n => n.Id == id, ct);
        if (notification is null) return Results.NotFound();
        notification.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> MarkAllNotificationsReadAsync(ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(user, out var userId)) return Results.Unauthorized();
        await VisibleNotifications(db, userId, user.IsInRole("Recruiter")).Where(n => n.ReadAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAt, DateTimeOffset.UtcNow), ct);
        return Results.NoContent();
    }
}
