using System.Security.Claims;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace CareerGap.Api.Administration;
public sealed record ReportRequest(string TargetType, Guid TargetId, string Reason);
public sealed record AdminAction(string Action, string Reason, long? RowVersion = null, long? JobRowVersion = null);
public static partial class AdminEndpoints
{
    private static Guid Uid(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static bool Valid(AdminAction input) => input.Reason?.Trim().Length is >= 3 and <= 500;
    private static void Audit(AppDb db, Guid actor, Guid target, string action, string reason) => db.AdminAudits.Add(new() { ActorId = actor, TargetId = target, Action = action, Reason = reason.Trim() });
    private static string LikePattern(string value) => "%" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
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
            if (await db.ContentReports.AnyAsync(x => x.ReporterId == uid && x.TargetType == request.TargetType && x.TargetId == request.TargetId && (x.Status == "open" || x.Status == "reviewed"), ct)) return Results.Problem(statusCode:409,title:"report_exists");
            var report = new ContentReport { ReporterId = uid, TargetId = request.TargetId, TargetType = request.TargetType, Reason = request.Reason.Trim() };
            db.ContentReports.Add(report); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.Created($"/api/reports/{report.Id}", new { report.Id, report.Status });
        }).RequireAuthorization(p => p.RequireRole("Candidate", "Recruiter")).RequireRateLimiting("matching");
        var admin = app.MapGroup("/api/admin").RequireAuthorization(p => p.RequireRole("Admin"));
        admin.MapGet("/accounts", async (int? page, string? query, string? status, string? role, AppDb db, CancellationToken ct) =>
        {
            if (status is not (null or "" or "all" or "active" or "suspended" or "deletion_pending" or "deleted") ||
                role is not (null or "" or "all" or "Candidate" or "Recruiter" or "Admin"))
                return Results.Problem(statusCode: 400, title: "invalid_admin_filter");
            var q = db.Users.AsNoTracking();
            if (status is not (null or "" or "all")) q = q.Where(x => x.AccountStatus == status);
            if (role is not (null or "" or "all"))
                q = q.Where(x => db.UserRoles.Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                    .Any(ur => ur.UserId == x.Id && ur.Name == role));
            if (!string.IsNullOrWhiteSpace(query))
            {
                var text = query.Trim()[..Math.Min(150, query.Trim().Length)];
                var pattern = LikePattern(text);
                q = q.Where(x => (x.Email != null && EF.Functions.ILike(x.Email, pattern, "\\")) ||
                    db.CandidateProfiles.Any(p => p.UserId == x.Id && EF.Functions.ILike(p.DisplayName, pattern, "\\")) ||
                    db.CommunityProfiles.Any(p => p.UserId == x.Id && EF.Functions.ILike(p.DisplayName, pattern, "\\")));
            }
            var currentPage = Math.Clamp(page ?? 1, 1, 10000);
            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((currentPage - 1) * 20).Take(20)
                .Select(x => new {
                    x.Id, x.Email, x.AccountStatus, x.CreatedAt, x.LastLoginAt,
                    displayName = db.CandidateProfiles.Where(p => p.UserId == x.Id).Select(p => p.DisplayName).FirstOrDefault()
                        ?? db.CommunityProfiles.Where(p => p.UserId == x.Id).Select(p => p.DisplayName).FirstOrDefault(),
                    roles = db.UserRoles.Where(r => r.UserId == x.Id).Join(db.Roles, r => r.RoleId, r => r.Id, (r, rObj) => rObj.Name).ToArray()
                }).ToArrayAsync(ct);
            return Results.Ok(new { items, total, page = currentPage, pageSize = 20 });
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
        admin.MapGet("/jobs", async (int? page, string? query, string? status, AppDb db, CancellationToken ct) =>
        {
            if (status is not (null or "" or "all" or "draft" or "published" or "closed" or "archived"))
                return Results.Problem(statusCode: 400, title: "invalid_admin_filter");
            var q = db.Jobs.AsNoTracking();
            if (status is not (null or "" or "all")) q = q.Where(x => x.Status == status);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var text = query.Trim()[..Math.Min(150, query.Trim().Length)];
                var pattern = LikePattern(text);
                q = q.Where(x => (x.CurrentVersion != null && EF.Functions.ILike(x.CurrentVersion.Title, pattern, "\\")) ||
                    (x.Company != null && EF.Functions.ILike(x.Company.Name, pattern, "\\")));
            }
            var currentPage = Math.Clamp(page ?? 1, 1, 10000);
            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
                .Skip((currentPage - 1) * 20).Take(20)
                .Select(x => new {
                    x.Id, title = x.CurrentVersion!.Title, company = x.Company!.Name, x.Status, x.RowVersion,
                    x.CreatedAt, x.PublishedAt, x.ArchivedByRole,
                    applicationCount = db.Applications.Count(a => a.JobId == x.Id)
                }).ToArrayAsync(ct);
            return Results.Ok(new { items, total, page = currentPage, pageSize = 20 });
        });
        admin.MapPost("/jobs/{id:guid}", async (Guid id, AdminAction input, ClaimsPrincipal user, AppDb db, CancellationToken ct) =>
        {
            if (!Valid(input) || input.Action is not ("archive" or "restore")) return Results.Problem(statusCode:400,title:"invalid_admin_action");
            await using var tx = await db.Database.BeginTransactionAsync(ct); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id={id} FOR UPDATE", ct);
            var job = await db.Jobs.SingleOrDefaultAsync(x => x.Id == id, ct); if (job is null) return Results.NotFound();
            if (input.RowVersion != job.RowVersion || (input.Action == "archive" ? job.Status is not ("published" or "closed") : job.Status != "archived")) return Results.Problem(statusCode:409,title:"job_conflict");
            if (input.Action == "archive")
            {
                job.PreviousStatus = job.Status;
                job.ArchivedByRole = "admin";
                job.Status = "archived";
                job.ClosedAt ??= DateTimeOffset.UtcNow;
            }
            else
            {
                var restoreDraft = job.PreviousStatus == "draft" && job.PublishedAt is null;
                job.Status = restoreDraft ? "draft" : "closed";
                if (restoreDraft) job.ClosedAt = null; else job.ClosedAt ??= DateTimeOffset.UtcNow;
                job.ArchivedByRole = null;
                job.PreviousStatus = null;
            }
            job.UpdatedAt = DateTimeOffset.UtcNow; job.RowVersion++;
            Audit(db, Uid(user), id, "job_"+input.Action, input.Reason); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
        });
        admin.MapGet("/reports", async (int? page, AppDb db, CancellationToken ct) =>
        {
            var currentPage = Math.Clamp(page ?? 1, 1, 10000);
            var q = db.ContentReports.AsNoTracking();
            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((currentPage - 1) * 20).Take(20)
                .Select(x => new { x.Id, x.TargetType, x.TargetId, x.Reason, x.Status, x.RowVersion, x.CreatedAt }).ToArrayAsync(ct);
            return Results.Ok(new { items, total, page = currentPage, pageSize = 20 });
        });
        MapReportWorkflow(admin);
        admin.MapGet("/audit", async (int? page, AppDb db, CancellationToken ct) =>
        {
            var currentPage = Math.Clamp(page ?? 1, 1, 10000);
            var q = db.AdminAudits.AsNoTracking();
            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((currentPage - 1) * 20).Take(20)
                .Select(x => new { x.Id, x.Action, x.Reason, x.CreatedAt, actor = db.Users.Where(u => u.Id == x.ActorId).Select(u => u.Email).FirstOrDefault() ?? "Tài khoản đã xóa" }).ToArrayAsync(ct);
            return Results.Ok(new { items, total, page = currentPage, pageSize = 20 });
        });
        admin.MapGet("/dashboard", async (int? days, AppDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var rangeDays = days ?? 30;
            if (rangeDays is not (7 or 14 or 30 or 90)) return Results.Problem(statusCode: 400, title: "invalid_dashboard_range");
            var now = clock.GetUtcNow();
            var startDate = DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(-(rangeDays - 1)));
            var startInstant = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

            var roleCounts = await db.UserRoles.Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .GroupBy(name => name).Select(g => new { role = g.Key, count = g.Count() }).ToDictionaryAsync(x => x.role!, x => x.count, ct);
            var userCounts = await db.Users.AsNoTracking().GroupBy(x => x.AccountStatus)
                .Select(g => new { status = g.Key, count = g.Count() }).ToDictionaryAsync(x => x.status, x => x.count, ct);
            var jobCounts = await db.Jobs.AsNoTracking().GroupBy(x => x.Status)
                .Select(g => new { status = g.Key, count = g.Count() }).ToDictionaryAsync(x => x.status, x => x.count, ct);
            var appCounts = await db.Applications.AsNoTracking().GroupBy(x => x.Status)
                .Select(g => new { status = g.Key, count = g.Count() }).ToDictionaryAsync(x => x.status, x => x.count, ct);
            var postCounts = await db.CommunityPosts.AsNoTracking().GroupBy(x => x.Status)
                .Select(g => new { status = g.Key, count = g.Count() }).ToDictionaryAsync(x => x.status, x => x.count, ct);

            var recruitmentReportsOpen = await db.ContentReports.AsNoTracking().CountAsync(x => x.Status == "open" || x.Status == "reviewed", ct);
            var communityReportsOpen = await db.CommunityReports.AsNoTracking().CountAsync(x => x.Status == "open" || x.Status == "reviewed", ct);

            var recentAppDates = await db.Applications.AsNoTracking().Where(x => x.SubmittedAt >= startInstant)
                .Select(x => x.SubmittedAt).ToListAsync(ct);
            var recentJobDates = await db.Jobs.AsNoTracking().Where(x => x.PublishedAt != null && x.PublishedAt >= startInstant && x.Status != "archived")
                .Select(x => x.PublishedAt!.Value).ToListAsync(ct);

            var appByDate = recentAppDates.GroupBy(d => DateOnly.FromDateTime(d.UtcDateTime)).ToDictionary(g => g.Key, g => g.Count());
            var jobByDate = recentJobDates.GroupBy(d => DateOnly.FromDateTime(d.UtcDateTime)).ToDictionary(g => g.Key, g => g.Count());

            var applicationsByDay = Enumerable.Range(0, rangeDays).Select(i => {
                var day = startDate.AddDays(i);
                return new { date = day.ToString("yyyy-MM-dd"), count = appByDate.GetValueOrDefault(day) };
            }).ToArray();
            var jobsPublishedByDay = Enumerable.Range(0, rangeDays).Select(i => {
                var day = startDate.AddDays(i);
                return new { date = day.ToString("yyyy-MM-dd"), count = jobByDate.GetValueOrDefault(day) };
            }).ToArray();

            var jobTags = await db.Jobs.AsNoTracking().Where(x => x.Status == "published" || x.Status == "closed")
                .Select(x => x.Tags).ToListAsync(ct);
            var topJobTags = jobTags.SelectMany(t => t ?? []).GroupBy(t => t, StringComparer.Ordinal)
                .Select(g => new { tag = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count).ThenBy(x => x.tag, StringComparer.Ordinal).Take(8).ToArray();

            var activeApps = appCounts.GetValueOrDefault("submitted") + appCounts.GetValueOrDefault("reviewing") + appCounts.GetValueOrDefault("shortlisted");
            return Results.Ok(new
            {
                days = rangeDays,
                totals = new
                {
                    users = new
                    {
                        total = userCounts.Values.Sum(),
                        active = userCounts.GetValueOrDefault("active"),
                        suspended = userCounts.GetValueOrDefault("suspended"),
                        deletionPending = userCounts.GetValueOrDefault("deletion_pending"),
                        deleted = userCounts.GetValueOrDefault("deleted"),
                        candidates = roleCounts.GetValueOrDefault("Candidate"),
                        recruiters = roleCounts.GetValueOrDefault("Recruiter"),
                        admins = roleCounts.GetValueOrDefault("Admin")
                    },
                    jobs = new
                    {
                        total = jobCounts.Values.Sum(),
                        draft = jobCounts.GetValueOrDefault("draft"),
                        published = jobCounts.GetValueOrDefault("published"),
                        closed = jobCounts.GetValueOrDefault("closed"),
                        archived = jobCounts.GetValueOrDefault("archived")
                    },
                    applications = new
                    {
                        total = appCounts.Values.Sum(),
                        active = activeApps,
                        rejected = appCounts.GetValueOrDefault("rejected"),
                        withdrawn = appCounts.GetValueOrDefault("withdrawn")
                    },
                    communityPosts = new
                    {
                        total = postCounts.Values.Sum(),
                        published = postCounts.GetValueOrDefault("published"),
                        pending = postCounts.GetValueOrDefault("pending"),
                        hidden = postCounts.GetValueOrDefault("hidden"),
                        deleted = postCounts.GetValueOrDefault("deleted")
                    },
                    reportsOpen = new { recruitment = recruitmentReportsOpen, community = communityReportsOpen }
                },
                series = new { applicationsByDay, jobsPublishedByDay, topJobTags }
            });
        });
        admin.MapGet("/usage", async (AppDb db, CancellationToken ct) => Results.Ok(new {
            aiBudgets = await db.BudgetPeriods.AsNoTracking().OrderByDescending(x => x.PeriodStart).Take(24).Select(x => new { x.Service, x.Unit, x.PeriodStart, x.PeriodEnd, x.LimitAmount, x.SpentAmount, x.ReservedAmount }).ToArrayAsync(ct),
            aiErrors = await db.AiJobs.Where(x => x.Status == "failed" || x.Status == "cancelled").Join(db.AiMethods, j => j.MethodVersionId, m => m.Id, (j,m) => new { m.Provider, j.ErrorCode }).GroupBy(x => new { x.Provider, x.ErrorCode }).Select(x => new { x.Key.Provider, x.Key.ErrorCode, count = x.Count() }).ToArrayAsync(ct),
            search = await db.LearningRequests.GroupBy(x => new { x.Provider, x.PeriodStart, x.PeriodEnd }).Select(x => new { x.Key.Provider, x.Key.PeriodStart, x.Key.PeriodEnd, actualCredits = x.Sum(r => r.ActualCredits ?? 0), reservedUnknownCredits = x.Sum(r => r.ActualCredits == null ? r.ReservedCredits : 0), requests = x.Count() }).ToArrayAsync(ct),
            email = await db.EmailDeliveries.GroupBy(x => new { x.Provider, x.PeriodStart, x.PeriodEnd, x.Status }).Select(x => new { x.Key.Provider, x.Key.PeriodStart, x.Key.PeriodEnd, x.Key.Status, count = x.Count() }).ToArrayAsync(ct)
        }));
    }
}
