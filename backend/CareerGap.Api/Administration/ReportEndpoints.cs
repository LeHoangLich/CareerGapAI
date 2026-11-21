using System.Security.Claims;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Administration;

public static partial class AdminEndpoints
{
    private static void MapReportWorkflow(RouteGroupBuilder admin)
    {
        admin.MapGet("/reports/inbox", async (string? status, string? targetType, int? page, AppDb db, CancellationToken ct) =>
        {
            if (page is < 1 or > 10000 || status is not (null or "all" or "open" or "reviewed" or "resolved" or "dismissed") || targetType is not (null or "all" or "job" or "public_resume"))
                return Results.Problem(statusCode:400, title:"invalid_report_filter");
            var q = db.ContentReports.AsNoTracking();
            if (status is not (null or "all")) q = q.Where(x => x.Status == status);
            if (targetType is not (null or "all")) q = q.Where(x => x.TargetType == targetType);
            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(((page ?? 1)-1)*10).Take(10)
                .Select(x => new { x.Id, x.TargetType, x.TargetId, x.Reason, x.Status, x.RowVersion, x.CreatedAt,
                    title = x.TargetType == "job" ? db.Jobs.Where(j => j.Id == x.TargetId).Select(j => j.CurrentVersion!.Title).FirstOrDefault() : null }).ToArrayAsync(ct);
            return Results.Ok(new { items, total, page = page ?? 1, pageSize = 10 });
        });
        admin.MapGet("/reports/{id:guid}", async (Guid id, AppDb db, CancellationToken ct) =>
        {
            var report = await db.ContentReports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (report is null) return Results.NotFound();
            // No resume content is loaded here: administrative access never grants private CV access.
            var job = report.TargetType == "job" ? await db.Jobs.AsNoTracking().Where(x => x.Id == report.TargetId)
                .Select(x => new { x.Id, x.Status, x.RowVersion, title = x.CurrentVersion!.Title, description = x.CurrentVersion.Description,
                    requirements = x.CurrentVersion.RequirementsText, benefits = x.CurrentVersion.BenefitsText, location = x.CurrentVersion.Location,
                    company = x.Company!.Name, ownerId = x.Company.OwnerUserId,
                    ownerStatus = db.Users.Where(u => u.Id == x.Company.OwnerUserId).Select(u => u.AccountStatus).FirstOrDefault(),
                    ownerProtected = db.UserRoles.Join(db.Roles, r => r.RoleId, r => r.Id, (r, role) => new { r.UserId, role.Name }).Any(r => r.UserId == x.Company.OwnerUserId && r.Name == "Admin") }).SingleOrDefaultAsync(ct) : null;
            var history = await db.AdminAudits.AsNoTracking().Where(x => x.TargetId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.Action, x.Reason, x.CreatedAt, actor = db.Users.Where(u => u.Id == x.ActorId).Select(u => u.Email).FirstOrDefault() ?? "Tài khoản đã xóa" }).ToArrayAsync(ct);
            return Results.Ok(new { report, job, history });
        });
        admin.MapPost("/reports/{id:guid}", ModerateReport);
    }

    private static async Task<IResult> ModerateReport(Guid id, AdminAction input, ClaimsPrincipal user, AppDb db, CancellationToken ct)
    {
        if (!Valid(input) || input.Action is not ("review" or "resolved" or "dismissed" or "hide" or "warn" or "suspend"))
            return Results.Problem(statusCode:400, title:"invalid_admin_action");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var report = await db.ContentReports.FromSqlInterpolated($"SELECT * FROM content_reports WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (report is null) return Results.NotFound();
        if (report.Status is not ("open" or "reviewed") || report.RowVersion != input.RowVersion || input.Action == "review" && report.Status != "open")
            return Results.Problem(statusCode:409, title:"report_conflict");
        var actor = Uid(user);
        if (input.Action is "hide" or "warn" or "suspend")
        {
            if (report.TargetType != "job") return Results.Problem(statusCode:400, title:"invalid_admin_action");
            var job = await db.Jobs.FromSqlInterpolated($"SELECT * FROM jobs WHERE id={report.TargetId} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (job is null) return Results.Problem(statusCode:409, title:"reported_job_unavailable");
            if (job.RowVersion != input.JobRowVersion) return Results.Problem(statusCode:409, title:"job_conflict");
            var ownerId = await db.Companies.Where(x => x.Id == job.CompanyId).Select(x => x.OwnerUserId).SingleAsync(ct);
            if (input.Action == "suspend")
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Id\"={ownerId} FOR UPDATE", ct);
                var owner = await db.Users.SingleAsync(x => x.Id == ownerId, ct);
                if (actor == ownerId || await db.UserRoles.Join(db.Roles, r => r.RoleId, r => r.Id, (r, role) => new { r.UserId, role.Name }).AnyAsync(x => x.UserId == ownerId && x.Name == "Admin", ct))
                    return Results.Problem(statusCode:409, title:"admin_account_protected");
                if (owner.AccountStatus != "active") return Results.Problem(statusCode:409, title:"account_state_conflict");
                owner.AccountStatus = "suspended"; owner.SecurityStamp = Guid.NewGuid().ToString(); owner.ConcurrencyStamp = Guid.NewGuid().ToString();
                Audit(db, actor, ownerId, "account_suspend", input.Reason);
            }
            else
            {
                if (job.Status is not ("published" or "closed" or "archived")) return Results.Problem(statusCode:409, title:"job_conflict");
                if (job.Status != "archived")
                {
                    job.PreviousStatus = job.Status; job.ArchivedByRole = "admin";
                    job.Status = "archived"; job.ClosedAt ??= DateTimeOffset.UtcNow; job.UpdatedAt = DateTimeOffset.UtcNow; job.RowVersion++;
                    Audit(db, actor, job.Id, "job_archive", input.Reason);
                }
                if (input.Action == "warn") db.Notifications.Add(new NotificationRecord {
                    RecipientUserId = ownerId, Title = "Cảnh cáo về tin tuyển dụng", NotificationType = "system_notice",
                    Body = $"Tin tuyển dụng {job.Id} đã bị ẩn. Lý do: {input.Reason.Trim()}", DeduplicationKey = "report-warning:" + report.Id });
            }
        }
        report.Status = input.Action is "review" or "suspend" ? "reviewed" : input.Action == "dismissed" ? "dismissed" : "resolved";
        report.RowVersion++;
        Audit(db, actor, id, "report_" + input.Action, input.Reason);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Results.NoContent();
    }
}
