using System.Net.Mail;
using System.Security.Claims;
using CareerGap.Api.Data;
using CareerGap.Api.Integrations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerGap.Api.Accounts;

public sealed record LoginRequest(string Email, string Password);
public sealed record RegistrationRequest(string Email, string Password, string Role, string? DisplayName);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record AccountDeletionPasswordRequest(string? Password);

public static class AccountEndpoints
{
    public static void MapAccounts(this WebApplication app)
    {
        app.MapGet("/api/auth/csrf", (HttpContext http, IAntiforgery antiforgery) => Results.Ok(new { token = antiforgery.GetAndStoreTokens(http).RequestToken }));
        app.MapGet("/api/auth/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            authenticated = user.Identity?.IsAuthenticated == true,
            userId = user.FindFirstValue(ClaimTypes.NameIdentifier),
            email = user.FindFirstValue(ClaimTypes.Email),
            roles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()
        }));
        app.MapPost("/api/auth/register", RegisterAsync).RequireRateLimiting("register");
        app.MapPost("/api/auth/login", async (LoginRequest input, UserManager<AppUser> users, SignInManager<AppUser> signin,
            AccountDeletionService deletions, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Email) || input.Email.Length > 254 || string.IsNullOrEmpty(input.Password) || input.Password.Length > 256) return Results.Problem(statusCode: 400, title: "invalid_credentials");
            var user = await users.FindByEmailAsync(input.Email);
            if (user is null || user.AccountStatus is not ("active" or "deletion_pending")) return Results.Problem(statusCode: 401, title: "invalid_credentials");
            var result = await signin.PasswordSignInAsync(user, input.Password, false, lockoutOnFailure: true);
            if (!result.Succeeded) return Results.Problem(statusCode: 401, title: "invalid_credentials");
            if (user.AccountStatus == "deletion_pending")
            {
                var state = await deletions.GetStateAsync(user.Id, ct);
                return Results.Ok(new { deletionPending = true, purgeAfter = state?.PurgeAfter });
            }
            return Results.NoContent();
        }).RequireRateLimiting("login");
        app.MapPost("/api/auth/logout", async (SignInManager<AppUser> signin) => { await signin.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
        app.MapPost("/api/auth/password/change", ChangePasswordAsync).RequireAuthorization();
        app.MapPost("/api/auth/password/forgot", ForgotPasswordAsync).RequireRateLimiting("recovery");
        app.MapPost("/api/auth/password/reset", ResetPasswordAsync).RequireRateLimiting("recovery");
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

        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
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
        if (await users.FindByEmailAsync(email) is not null) return Results.Problem(statusCode: 409, title: "email_exists");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var role = await roles.FindByNameAsync(roleName);
        if (role is null)
        {
            var roleResult = await roles.CreateAsync(new IdentityRole<Guid>(roleName));
            if (!roleResult.Succeeded && await roles.FindByNameAsync(roleName) is null) return Results.Problem(statusCode: 500, title: "account_setup_failed");
        }

        var account = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email };
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
            var displayName = string.IsNullOrWhiteSpace(input.DisplayName) ? email.Split('@')[0] : input.DisplayName.Trim();
            if (displayName.Length > 150) return Results.Problem(statusCode: 400, title: "invalid_display_name");
            db.CandidateProfiles.Add(new CandidateProfile { UserId = account.Id, DisplayName = displayName });
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
                var baseUrl = configuration["Recovery:BaseUrl"] ?? "http://127.0.0.1:5173/";
                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBase) && parsedBase is not null && parsedBase.Scheme == "http" && parsedBase.IsLoopback)
                {
                    var builder = new UriBuilder(new Uri(parsedBase, "/"));
                    builder.Query = $"resetEmail={Uri.EscapeDataString(account.Email!)}&resetToken={Uri.EscapeDataString(token)}";
                    await emailSender.SendPasswordRecoveryAsync(account.Email!, builder.Uri, ct);
                }
            }
        }
        return Results.NoContent();
    }

    private static async Task<IResult> ResetPasswordAsync(ResetPasswordRequest input, UserManager<AppUser> users)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrWhiteSpace(input.Token) || input.NewPassword is null || input.NewPassword.Length is < 12 or > 256)
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

    private static bool TryUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
