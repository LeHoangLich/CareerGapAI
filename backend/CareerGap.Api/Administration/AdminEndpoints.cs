using System.Security.Claims;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace CareerGap.Api.Administration;
public sealed record ReportRequest(string TargetType, Guid TargetId, string Reason);
public sealed record AdminAction(string Action, string Reason, long? RowVersion = null);
public static class AdminEndpoints
{
    private static Guid Uid(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static bool Valid(AdminAction input) => input.Reason?.Trim().Length is >= 3 and <= 500;
    private static void Audit(AppDb db, Guid actor, Guid target, string action, string reason) => db.AdminAudits.Add(new() { ActorId = actor, TargetId = target, Action = action, Reason = reason.Trim() });
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/reports", async (ReportRequest request, ClaimsPrincipal user, AppDb db, CancellationToken ct) =>
        {
            if (request.Reason?.Trim().Length is not (>= 3 and <= 1000) || request.TargetType is not ("job" or "public_resume")) return Results.Problem(statusCode:400, title:"invalid_report");
            var accessible = request.TargetType == "job"
                ? await db.Jobs.AnyAsync(x => x.Id == request.TargetId && (x.Status == "published" || x.Status == "closed") && x.Company!.ArchivedAt == null && db.Users.Any(u => u.Id == x.Company.OwnerUserId && u.AccountStatus == "active"), ct)
                : await db.CandidatePublicResumes.AnyAsync(x => x.ResumeVersionId == request.TargetId && (user.IsInRole("Recruiter") || x.Status == "ready" && x.RedactedFileId != null && x.RedactionVerifiedAt != null) && db.Users.Any(u => u.Id == x.CandidateId && u.AccountStatus == "active") &&
                    db.ResumeVersions.Any(v => v.Id == x.ResumeVersionId && v.CandidateId == x.CandidateId && v.DocumentHash == x.SourceHash && db.StoredFiles.Any(f => f.Id == v.FileId && f.OwnerUserId == x.CandidateId && f.Status == "ready" && f.Sha256 == x.SourceHash)), ct);
            if (!accessible) return Results.NotFound();
            var uid = Uid(user);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({'r' + uid.ToString()}, 0))", ct);
            if (await db.ContentReports.CountAsync(x => x.ReporterId == uid && x.CreatedAt > DateTimeOffset.UtcNow.AddDays(-1), ct) >= 10) return Results.Problem(statusCode:429,title:"report_limit");
            var report = new ContentReport { ReporterId = uid, TargetId = request.TargetId, TargetType = request.TargetType, Reason = request.Reason.Trim() };
            db.ContentReports.Add(report); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Created($"/api/reports/{report.Id}", new { report.Id, report.Status });
        }).RequireAuthorization(p => p.RequireRole("Candidate", "Recruiter")).RequireRateLimiting("matching");
        var admin = app.MapGroup("/api/admin").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet("/accounts", async (int? page, string? query, AppDb db, CancellationToken ct) =>
        {
            var q = db.Users.AsNoTracking(); if (!string.IsNullOrWhiteSpace(query)) { var text = query.Trim()[..Math.Min(150, query.Trim().Length)]; q = q.Where(x => x.Email != null && x.Email.Contains(text)); }
            return Results.Ok(await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Math.Clamp(page ?? 1,1,10000)-1)*20).Take(20)
                .Select(x => new { x.Id, x.Email, x.AccountStatus, roles = db.UserRoles.Where(r => r.UserId == x.Id).Join(db.Roles, r => r.RoleId, r => r.Id, (r, role) => role.Name).ToArray() }).ToArrayAsync(ct));
        });
        admin.MapPost("/accounts/{id:guid}", async (Guid id, AdminAction input, ClaimsPrincipal user, AppDb db, CancellationToken ct) =>
        {
            if (!Valid(input) || input.Action is not ("suspend" or "activate")) return Results.Problem(statusCode:400,title:"invalid_admin_action");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Id\" = {id} FOR UPDATE", ct);
            var account = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct); if (account is null) return Results.NotFound();
            if (id == Uid(user) || await db.UserRoles.Join(db.Roles, r => r.RoleId, r => r.Id, (r, role) => new { r.UserId, role.Name }).AnyAsync(x => x.UserId == id && x.Name == "Admin", ct)) return Results.Problem(statusCode:409,title:"admin_account_protected");
            if (account.AccountStatus != (input.Action == "suspend" ? "active" : "suspended")) return Results.Problem(statusCode:409,title:"account_state_conflict");
            account.AccountStatus = input.Action == "suspend" ? "suspended" : "active"; account.SecurityStamp = Guid.NewGuid().ToString(); account.ConcurrencyStamp = Guid.NewGuid().ToString();
            Audit(db, Uid(user), id, "account_"+input.Action, input.Reason); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
        });
        admin.MapGet("/jobs", async (int? page, AppDb db, CancellationToken ct) => Results.Ok(await db.Jobs.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((Math.Clamp(page ?? 1,1,10000)-1)*20).Take(20).Select(x => new { x.Id, title = x.CurrentVersion!.Title, company = x.Company!.Name, x.Status, x.RowVersion }).ToArrayAsync(ct)));
        admin.MapPost("/jobs/{id:guid}", async (Guid id, AdminAction input, ClaimsPrincipal user, AppDb db, CancellationToken ct) =>
        {
            if (!Valid(input) || input.Action is not ("archive" or "restore")) return Results.Problem(statusCode:400,title:"invalid_admin_action");
            await using var tx = await db.Database.BeginTransactionAsync(ct); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id={id} FOR UPDATE", ct);
            var job = await db.Jobs.SingleOrDefaultAsync(x => x.Id == id, ct); if (job is null) return Results.NotFound();
            if (input.RowVersion != job.RowVersion || (input.Action == "archive" ? job.Status is not ("published" or "closed") : job.Status != "archived")) return Results.Problem(statusCode:409,title:"job_conflict");
            job.Status = input.Action == "archive" ? "archived" : "closed"; job.ClosedAt ??= DateTimeOffset.UtcNow; job.UpdatedAt = DateTimeOffset.UtcNow; job.RowVersion++;
            Audit(db, Uid(user), id, "job_"+input.Action, input.Reason); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
        });
        admin.MapGet("/reports", async (int? page, AppDb db, CancellationToken ct) => Results.Ok(await db.ContentReports.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Math.Clamp(page ?? 1,1,10000)-1)*20).Take(20)
            .Select(x => new { x.Id, x.TargetType, x.TargetId, x.Reason, x.Status, x.RowVersion, x.CreatedAt }).ToArrayAsync(ct)));
        admin.MapPost("/reports/{id:guid}", async (Guid id, AdminAction input, ClaimsPrincipal user, AppDb db, CancellationToken ct) =>
        {
            if (!Valid(input) || input.Action is not ("resolved" or "dismissed")) return Results.Problem(statusCode:400,title:"invalid_admin_action");
            await using var tx = await db.Database.BeginTransactionAsync(ct); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM content_reports WHERE id={id} FOR UPDATE", ct);
            var report = await db.ContentReports.SingleOrDefaultAsync(x => x.Id == id, ct); if (report is null) return Results.NotFound();
            if (report.Status != "open" || report.RowVersion != input.RowVersion) return Results.Problem(statusCode:409,title:"report_conflict");
            report.Status = input.Action; report.RowVersion++; Audit(db, Uid(user), id, "report_"+input.Action, input.Reason); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
        });
        admin.MapGet("/audit", async (int? page, AppDb db, CancellationToken ct) => Results.Ok(await db.AdminAudits.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((Math.Clamp(page ?? 1,1,10000)-1)*20).Take(20)
            .Select(x => new { x.Id, x.Action, x.Reason, x.CreatedAt, actor = db.Users.Where(u => u.Id == x.ActorId).Select(u => u.Email).FirstOrDefault() ?? "Tài khoản đã xóa" }).ToArrayAsync(ct)));
        admin.MapGet("/usage", async (AppDb db, CancellationToken ct) => Results.Ok(new {
            aiBudgets = await db.BudgetPeriods.AsNoTracking().OrderByDescending(x => x.PeriodStart).Take(24).Select(x => new { x.Service, x.Unit, x.PeriodStart, x.PeriodEnd, x.LimitAmount, x.SpentAmount, x.ReservedAmount }).ToArrayAsync(ct),
            aiErrors = await db.AiJobs.Where(x => x.Status == "failed" || x.Status == "cancelled").Join(db.AiMethods, j => j.MethodVersionId, m => m.Id, (j,m) => new { m.Provider, j.ErrorCode }).GroupBy(x => new { x.Provider, x.ErrorCode }).Select(x => new { x.Key.Provider, x.Key.ErrorCode, count = x.Count() }).ToArrayAsync(ct),
            search = await db.LearningRequests.GroupBy(x => new { x.Provider, x.PeriodStart, x.PeriodEnd }).Select(x => new { x.Key.Provider, x.Key.PeriodStart, x.Key.PeriodEnd, actualCredits = x.Sum(r => r.ActualCredits ?? 0), reservedUnknownCredits = x.Sum(r => r.ActualCredits == null ? r.ReservedCredits : 0), requests = x.Count() }).ToArrayAsync(ct),
            email = await db.EmailDeliveries.GroupBy(x => new { x.Provider, x.PeriodStart, x.PeriodEnd, x.Status }).Select(x => new { x.Key.Provider, x.Key.PeriodStart, x.Key.PeriodEnd, x.Key.Status, count = x.Count() }).ToArrayAsync(ct)
        }));
    }
}
