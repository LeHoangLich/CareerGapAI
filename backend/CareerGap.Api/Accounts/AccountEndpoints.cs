using System.Security.Claims;
using CareerGap.Api.Data;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace CareerGap.Api.Accounts;

public sealed record LoginRequest(string Email, string Password);
public static class AccountEndpoints
{
    public static void MapAccounts(this WebApplication app)
    {
        app.MapGet("/api/auth/csrf", (HttpContext http, IAntiforgery antiforgery) => Results.Ok(new { token = antiforgery.GetAndStoreTokens(http).RequestToken }));
        app.MapGet("/api/auth/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            authenticated = user.Identity?.IsAuthenticated == true,
            email = user.FindFirstValue(ClaimTypes.Email),
            roles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray()
        }));
        app.MapPost("/api/auth/login", async (LoginRequest input, UserManager<AppUser> users, SignInManager<AppUser> signin) =>
        {
            if (input.Email.Length > 254 || input.Password.Length > 256) return Results.Problem(statusCode: 400, title: "invalid_credentials");
            var user = await users.FindByEmailAsync(input.Email);
            if (user is null || user.AccountStatus != "active") return Results.Problem(statusCode: 401, title: "invalid_credentials");
            var result = await signin.PasswordSignInAsync(user, input.Password, false, lockoutOnFailure: true);
            return result.Succeeded ? Results.NoContent() : Results.Problem(statusCode: 401, title: "invalid_credentials");
        }).RequireRateLimiting("login");
        app.MapPost("/api/auth/logout", async (SignInManager<AppUser> signin) => { await signin.SignOutAsync(); return Results.NoContent(); }).RequireAuthorization();
    }
}
