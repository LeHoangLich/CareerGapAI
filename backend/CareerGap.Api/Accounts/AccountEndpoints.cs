using System.Net.Mail;
using System.Security.Claims;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Integrations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CareerGap.Api.Accounts;

public sealed record LoginRequest(string Email, string Password);
public sealed record RegistrationRequest(string Email, string Password, string Role, string? DisplayName);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record AccountDeletionPasswordRequest(string? Password);
public sealed record AccountProfileUpdateRequest(string DisplayName, Guid? AvatarImageId, bool NotifyRecruitment, bool NotifyCommunity);

public static class AccountEndpoints
{
    public static void MapAccounts(this WebApplication app)
    {
        var localFakeMailbox = (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) &&
            app.Services.GetRequiredService<IOptions<EmailOptions>>().Value.Provider == "Fake";
        app.MapGet("/api/auth/csrf", (HttpContext http, IAntiforgery antiforgery) => Results.Ok(new { token = antiforgery.GetAndStoreTokens(http).RequestToken }));
        app.MapGet("/api/auth/me", async (ClaimsPrincipal user, AppDb db, CancellationToken ct) =>
        {
            if (user.Identity?.IsAuthenticated != true || !TryUserId(user, out var uid))
                return Results.Ok(new { authenticated = false, userId = (string?)null, email = (string?)null, roles = Array.Empty<string>() });
            var profile = await BuildProfileDtoAsync(uid, user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray(), db, ct);
            if (profile is null)
                return Results.Ok(new { authenticated = false, userId = (string?)null, email = (string?)null, roles = Array.Empty<string>() });
            return Results.Ok(new
            {
                authenticated = true,
                userId = uid.ToString(),
                profile.Email,
                profile.Roles,
                profile.DisplayName,
                profile.AvatarImageId,
                profile.AvatarUrl,
                profile.EmailConfirmed,
                profile.LastLoginAt,
                profile.NotifyRecruitment,
                profile.NotifyCommunity
            });
        });
        app.MapGet("/api/account/profile", async (ClaimsPrincipal user, AppDb db, CancellationToken ct) =>
        {
            if (!TryUserId(user, out var uid)) return Results.Unauthorized();
            var profile = await BuildProfileDtoAsync(uid, user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray(), db, ct);
            return profile is null ? Results.Unauthorized() : Results.Ok(profile);
        }).RequireAuthorization();
        app.MapPut("/api/account/profile", UpdateProfileAsync).RequireAuthorization().RequireRateLimiting("community");
        app.MapGet("/api/account/ai-quota", GetAiQuotaAsync).RequireAuthorization();
        app.MapPost("/api/auth/register", RegisterAsync).RequireRateLimiting("register");
        app.MapPost("/api/auth/login", async (LoginRequest input, UserManager<AppUser> users, SignInManager<AppUser> signin,
            AccountDeletionService deletions, AppDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 || string.IsNullOrEmpty(input.Password) || input.Password.Length > 256) return Results.Problem(statusCode: 400, title: "invalid_credentials");
            var user = await users.FindByEmailAsync(input.Email);
            if (user is null || user.AccountStatus is not ("active" or "deletion_pending")) return Results.Problem(statusCode: 401, title: "invalid_credentials");
            var result = await signin.PasswordSignInAsync(user, input.Password, false, lockoutOnFailure: true);
            if (!result.Succeeded) return Results.Problem(statusCode: 401, title: "invalid_credentials");
            await db.Users.Where(x => x.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, clock.GetUtcNow()), ct);
            if (user.AccountStatus == "deletion_pending")
            {
                var state = await deletions.GetStateAsync(user.Id, ct);
                return Results.Ok(new { deletionPending = true, purgeAfter = state?.PurgeAfter });
            }
            return Results.NoContent();
        }).RequireRateLimiting("login");
        app.MapPost("/api/auth/logout", async (SignInManager<AppUser> signin) => { await signin.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
        app.MapPost("/api/auth/logout-other-devices", async (ClaimsPrincipal principal, UserManager<AppUser> users, SignInManager<AppUser> signin) =>
        {
            var account = await users.GetUserAsync(principal);
            if (account is null || account.AccountStatus != "active") return Results.Problem(statusCode: 401, title: "invalid_session");
            await users.UpdateSecurityStampAsync(account);
            await signin.RefreshSignInAsync(account);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("recovery");
        app.MapPost("/api/auth/password/change", ChangePasswordAsync).RequireAuthorization();
        app.MapPost("/api/auth/password/forgot", ForgotPasswordAsync).RequireRateLimiting("recovery");
        app.MapPost("/api/auth/password/reset", ResetPasswordAsync).RequireRateLimiting("recovery");
        app.MapGet("/api/auth/recovery-options", () => Results.Ok(new { localMailboxAvailable = localFakeMailbox }));
        app.MapPost("/api/account/deletion-request", async (AccountDeletionPasswordRequest input, ClaimsPrincipal principal,
            AccountDeletionService deletions, CancellationToken ct) =>
        {
            if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
            if (!principal.IsInRole("Candidate") && !principal.IsInRole("Recruiter")) return Results.Forbid();
            var result = await deletions.RequestAsync(userId, input.Password, ct);
            if (result.Error is not null) return MapDeletionError(result.Error);
            var request = result.Request!;
            return Results.Accepted("/api/account/deletion-request", new AccountDeletionState(request.Status, request.RequestedAt, request.PurgeAfter));
        }).RequireAuthorization().RequireRateLimiting("recovery");
        app.MapGet("/api/account/deletion-request", async (ClaimsPrincipal principal, AccountDeletionService deletions, CancellationToken ct) =>
        {
            if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
            var state = await deletions.GetStateAsync(userId, ct);
            return state is null ? Results.NotFound() : Results.Ok(state);
        }).RequireAuthorization();
        app.MapPost("/api/account/deletion-request/cancel", async (AccountDeletionPasswordRequest input, ClaimsPrincipal principal,
            AccountDeletionService deletions, SignInManager<AppUser> signin, CancellationToken ct) =>
        {
            if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
            var result = await deletions.CancelAsync(userId, input.Password, ct);
            if (result.Error is not null) return MapDeletionError(result.Error);
            await signin.RefreshSignInAsync(result.User!);
            return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("recovery");

        if (localFakeMailbox)
        {
            app.MapGet("/api/dev/mailbox", (HttpContext http, LocalMailOutbox outbox) =>
            {
                if (app.Environment.IsDevelopment() && (http.Connection.RemoteIpAddress is not { } address || !System.Net.IPAddress.IsLoopback(address))) return Results.NotFound();
                return Results.Ok(outbox.Drain());
            });
        }
    }

    private static async Task<IResult> RegisterAsync(RegistrationRequest input, AppDb db, UserManager<AppUser> users,
        RoleManager<IdentityRole<Guid>> roles, CancellationToken ct)
    {
        var email = input.Email?.Trim();
        var roleName = input.Role?.Trim();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !MailAddress.TryCreate(email, out var parsed) || !string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase))
            return Results.Problem(statusCode: 400, title: "invalid_email");
        if (roleName is not ("Candidate" or "Recruiter")) return Results.Problem(statusCode: 400, title: "invalid_role");
        if (input.Password is null || input.Password.Length < 12 || input.Password.Length > 256) return Results.Problem(statusCode: 400, title: "password_policy");
        if (roleName == "Recruiter" && !CareerGap.Api.Recruitment.BusinessIdentityEndpoints.CorporateEmail(email)) return Results.Problem(statusCode:400,title:"business_email_required");
        if (await users.FindByEmailAsync(email) is not null) return Results.Problem(statusCode: 409, title: "email_exists");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var role = await roles.FindByNameAsync(roleName);
        if (role is null)
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!roleResult.Succeeded && await roles.FindByNameAsync(roleName) is null) return Results.Problem(statusCode: 500, title: "account_setup_failed");
        }

        var account = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = roleName == "Candidate"
                ? (string.IsNullOrWhiteSpace(input.DisplayName) ? email.Split('@')[0] : input.DisplayName.Trim())
                : (string.IsNullOrWhiteSpace(input.DisplayName) ? null : input.DisplayName.Trim())
        };
        if (account.DisplayName is { Length: > 150 }) return Results.Problem(statusCode: 400, title: "invalid_display_name");
        var created = await users.CreateAsync(account, input.Password);
        if (!created.Succeeded)
        {
            if (created.Errors.Any(e => e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase))) return Results.Problem(statusCode: 409, title: "email_exists");
            return Results.Problem(statusCode: 400, title: "password_policy");
        }
        var added = await users.AddToRoleAsync(account, roleName);
        if (!added.Succeeded) return Results.Problem(statusCode: 500, title: "account_setup_failed");
        if (roleName == "Candidate")
        {
            db.CandidateProfiles.Add(new CandidateProfile { UserId = account.Id, DisplayName = account.DisplayName! });
            await db.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return Results.Created("/api/auth/me", new { role = roleName });
    }

    private static async Task<IResult> ChangePasswordAsync(ChangePasswordRequest input, ClaimsPrincipal principal,
        UserManager<AppUser> users, SignInManager<AppUser> signin)
    {
        if (string.IsNullOrEmpty(input.CurrentPassword) || input.NewPassword is null || input.NewPassword.Length is < 12 or > 256)
            return Results.Problem(statusCode: 400, title: "password_policy");
        var account = await users.GetUserAsync(principal);
        if (account is null || account.AccountStatus != "active") return Results.Problem(statusCode: 401, title: "invalid_session");
        var result = await users.ChangePasswordAsync(account, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded) return Results.Problem(statusCode: 400, title: result.Errors.Any(e => e.Code.Contains("PasswordMismatch", StringComparison.OrdinalIgnoreCase)) ? "current_password_invalid" : "password_policy");
        await signin.RefreshSignInAsync(account);
        return Results.NoContent();
    }

    private static async Task<IResult> ForgotPasswordAsync(ForgotPasswordRequest input, HttpContext http,
        UserManager<AppUser> users, IEmailSender emailSender, IConfiguration configuration, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(input.Email) && input.Email.Length <= 254)
        {
            var account = await users.FindByEmailAsync(input.Email.Trim());
            if (account is { AccountStatus: "active" })
            {
                var token = await users.GeneratePasswordResetTokenAsync(account);
                var configuredBase = configuration["Recovery:BaseUrl"];
                var requestOrigin = Uri.TryCreate(http.Request.Headers.Origin.ToString(), UriKind.Absolute, out var originUri)
                    && originUri.Scheme == "http" && originUri.IsLoopback
                    ? originUri.GetLeftPart(UriPartial.Authority) : null;
                var baseUrl = !string.IsNullOrWhiteSpace(configuredBase) ? configuredBase : requestOrigin ?? "http://127.0.0.1:5173/";
                var allowHttps = !string.IsNullOrWhiteSpace(configuredBase) &&
                    bool.TryParse(configuration["Recovery:AllowHttpsOrigin"], out var allowHttpsFlag) && allowHttpsFlag;
                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBase) && parsedBase is not null &&
                    ((parsedBase.Scheme == "http" && parsedBase.IsLoopback) ||
                     (allowHttps && parsedBase.Scheme == "https" && parsedBase.HostNameType == UriHostNameType.Dns && !parsedBase.IsLoopback)))
                {
                    var builder = new UriBuilder(new Uri(parsedBase, "/"));
                    builder.Query = $"resetEmail={Uri.EscapeDataString(account.Email!)}&resetToken={Uri.EscapeDataString(token)}";
                    // Keep the same public response for missing accounts and delivery/configuration failures.
                    try { await emailSender.SendPasswordRecoveryAsync(account.Email!, builder.Uri, ct); }
                    catch (Exception e) when (e is IntegrationError or HttpRequestException or OperationCanceledException) { }
                }
            }
        }
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest input, UserManager<AppUser> users)
    {
        if (input.NewPassword is null || input.NewPassword.Length is < 12 or > 256)
            return Results.Problem(statusCode: 400, title: "password_policy");
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Token))
            return Results.Problem(statusCode: 400, title: "invalid_or_expired_reset");
        var account = await users.FindByEmailAsync(input.Email.Trim());
        if (account is null || account.AccountStatus != "active") return Results.Problem(statusCode: 400, title: "invalid_or_expired_reset");
        var result = await users.ResetPasswordAsync(account, input.Token, input.NewPassword);
        return result.Succeeded ? Results.NoContent() : Results.Problem(statusCode: 400, title: "invalid_or_expired_reset");
    }

    private static IResult MapDeletionError(string error) => error switch
    {
        "current_password_invalid" => Results.Problem(statusCode: 400, title: error),
        "deletion_not_cancellable" => Results.Problem(statusCode: 409, title: error),
        _ => Results.Problem(statusCode: 409, title: error)
    };

    private sealed record AccountProfileDto(
        string? Email,
        string[] Roles,
        string? DisplayName,
        Guid? AvatarImageId,
        string? AvatarUrl,
        bool EmailConfirmed,
        DateTimeOffset? LastLoginAt,
        bool NotifyRecruitment,
        bool NotifyCommunity);

    private static async Task<AccountProfileDto?> BuildProfileDtoAsync(Guid uid, string[] roles, AppDb db, CancellationToken ct)
    {
        var account = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == uid, ct);
        if (account is null) return null;
        var candidateName = await db.CandidateProfiles.AsNoTracking().Where(x => x.UserId == uid).Select(x => x.DisplayName).SingleOrDefaultAsync(ct);
        var community = await db.CommunityProfiles.AsNoTracking().Where(x => x.UserId == uid).Select(x => new { x.DisplayName, x.AvatarId }).SingleOrDefaultAsync(ct);
        var displayName = !string.IsNullOrWhiteSpace(account.DisplayName) ? account.DisplayName
            : !string.IsNullOrWhiteSpace(candidateName) ? candidateName
            : community?.DisplayName;
        var avatarImageId = account.AvatarImageId ?? community?.AvatarId;
        var avatarUrl = avatarImageId is { } aid ? $"/api/community/images/{aid}" : null;
        return new AccountProfileDto(account.Email, roles, displayName, avatarImageId, avatarUrl,
            account.EmailConfirmed, account.LastLoginAt, account.NotifyRecruitment, account.NotifyCommunity);
    }

    private static async Task<IResult> UpdateProfileAsync(AccountProfileUpdateRequest input, ClaimsPrincipal principal, AppDb db, TimeProvider clock, CancellationToken ct)
    {
        if (!TryUserId(principal, out var uid)) return Results.Unauthorized();
        var displayName = input.DisplayName?.Trim() ?? "";
        if (displayName.Length is < 1 or > 150) return Results.Problem(statusCode: 400, title: "invalid_display_name");
        if (input.AvatarImageId is { } avatarId && !await db.CommunityImages.AnyAsync(x => x.Id == avatarId && x.OwnerId == uid, ct))
            return Results.Problem(statusCode: 400, title: "community_image_not_owned");

        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {uid} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (account is null || account.AccountStatus != "active") return Results.Unauthorized();

        account.DisplayName = displayName;
        account.AvatarImageId = input.AvatarImageId;
        account.NotifyRecruitment = input.NotifyRecruitment;
        account.NotifyCommunity = input.NotifyCommunity;

        var candidate = await db.CandidateProfiles.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (candidate is not null)
        {
            candidate.DisplayName = displayName;
            candidate.UpdatedAt = now;
        }
        var community = await db.CommunityProfiles.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        if (community is not null)
        {
            community.DisplayName = displayName;
            community.AvatarId = input.AvatarImageId;
            community.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var roles = principal.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
        return Results.Ok(new AccountProfileDto(account.Email, roles, account.DisplayName, account.AvatarImageId,
            account.AvatarImageId is { } aid ? $"/api/community/images/{aid}" : null,
            account.EmailConfirmed, account.LastLoginAt, account.NotifyRecruitment, account.NotifyCommunity));
    }

    private static async Task<IResult> GetAiQuotaAsync(ClaimsPrincipal principal, AppDb db, IOptions<AiOptions> options, TimeProvider clock, CancellationToken ct)
    {
        if (!TryUserId(principal, out var uid)) return Results.Unauthorized();
        var opt = options.Value;
        var now = clock.GetUtcNow();
        DateTimeOffset start;
        DateTimeOffset end;
        if (opt.Provider == "Fake")
        {
            start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            end = start.AddMonths(1);
        }
        else if (DateTimeOffset.TryParse(opt.PeriodStart, out var parsedStart) && DateTimeOffset.TryParse(opt.PeriodEnd, out var parsedEnd))
        {
            start = parsedStart.ToUniversalTime();
            end = parsedEnd.ToUniversalTime();
        }
        else
        {
            start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            end = start.AddMonths(1);
        }

        var budget = await db.BudgetPeriods.AsNoTracking().SingleOrDefaultAsync(x => x.Service == opt.Provider && x.PeriodStart == start, ct);
        var activeJobs = await db.AiJobs.CountAsync(x => x.RequestedBy == uid && (x.Status == "queued" || x.Status == "running"), ct);
        var maxActiveJobs = Math.Clamp(opt.UserMaxActiveJobs, 1, 10);

        decimal usedAmount = 0m;
        decimal reservedAmount = 0m;
        int jobsCountInPeriod = 0;

        if (budget is not null)
        {
            var reservations = await (from r in db.BudgetReservations.AsNoTracking()
                join j in db.AiJobs.AsNoTracking() on r.AiJobId equals j.Id
                where j.RequestedBy == uid && r.BudgetPeriodId == budget.Id
                select new { r.AiJobId, r.ReservedRemaining, j.Status }).ToListAsync(ct);

            var jobIds = reservations.Select(x => x.AiJobId).ToArray();
            if (jobIds.Length > 0)
            {
                usedAmount = await db.ExternalCallAttempts.AsNoTracking()
                    .Where(x => jobIds.Contains(x.AiJobId))
                    .SumAsync(x => x.ActualAmount ?? 0m, ct);
            }
            reservedAmount = reservations.Sum(x => x.ReservedRemaining);
            jobsCountInPeriod = reservations.Count(x => x.Status == "succeeded");
        }

        var limitAmount = opt.Provider == "Fake" ? opt.FakeUserTestCreditLimit : Math.Min(opt.UserPeriodLimit, opt.MonthlyLimit);
        var effectiveSystemLimit = budget is null
            ? (opt.Provider == "Fake" ? 1000m : opt.MonthlyLimit)
            : (opt.Provider == "Fake" ? budget.LimitAmount : Math.Min(budget.LimitAmount, opt.MonthlyLimit));
        var systemExhausted = limitAmount <= 0 || (budget is not null && budget.SpentAmount + budget.ReservedAmount >= effectiveSystemLimit);
        var usedPercent = limitAmount > 0 ? Math.Min(100m, Math.Round((usedAmount + reservedAmount) / limitAmount * 100m, 1)) : 100m;

        return Results.Ok(new
        {
            enabled = opt.Enabled,
            provider = opt.Provider,
            unit = opt.Provider == "Fake" ? "test_credit" : "usd",
            planLabel = "Gói Cơ bản",
            periodStart = start,
            periodEnd = end,
            usedAmount,
            reservedAmount,
            limitAmount,
            usedPercent,
            jobsCountInPeriod,
            activeJobs,
            maxActiveJobs,
            systemExhausted
        });
    }

    private static bool TryUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
