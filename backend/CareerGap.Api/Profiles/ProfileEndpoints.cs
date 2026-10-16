using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Profiles;

public sealed record ProfileUpdateRequest(
    long RowVersion,
    string DisplayName,
    string? Headline,
    string? Summary,
    string? Location,
    string? ContactEmail,
    string? ContactPhone,
    string? DesiredRole,
    string? PreferredWorkMode,
    decimal? ExperienceYears,
    JsonElement Details,
    string[] SharedFields);

public static class ProfileEndpoints
{
    private static readonly HashSet<string> SharedFieldAllowlist = new(StringComparer.Ordinal)
    {
        "display_name", "headline", "summary", "location", "contact_email", "contact_phone",
        "desired_role", "preferred_work_mode", "experience_years", "skills", "experience",
        "education", "projects", "certifications"
    };
    private static readonly HashSet<string> DetailAllowlist = new(StringComparer.Ordinal)
    { "skills", "experience", "education", "projects", "certifications" };
    private static readonly HashSet<string> WorkModes = new(StringComparer.Ordinal)
    { "onsite", "hybrid", "remote", "unspecified" };

    public static void MapProfiles(this WebApplication app)
    {
        app.MapGet("/api/profile/me", async (ClaimsPrincipal principal, AppDb db) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
            var profile = await db.CandidateProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId);
            return profile is null ? Results.NotFound() : Results.Ok(ToFullDto(profile, await db.CandidateProfileVisibility.AsNoTracking().Where(x => x.CandidateId == userId).Select(x => x.FieldKey).ToArrayAsync()));
        }).RequireAuthorization(policy => policy.RequireRole("Candidate"));

        app.MapPut("/api/profile/me", UpdateAsync).RequireAuthorization(policy => policy.RequireRole("Candidate"));

        app.MapGet("/api/profiles/{candidateId:guid}", async (Guid candidateId, AppDb db) =>
        {
            if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == candidateId && x.AccountStatus == "active")) return Results.NotFound();
            var profile = await db.CandidateProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == candidateId);
            if (profile is null) return Results.NotFound();
            var shared = (await db.CandidateProfileVisibility.AsNoTracking().Where(x => x.CandidateId == candidateId).Select(x => x.FieldKey).ToArrayAsync()).ToHashSet(StringComparer.Ordinal);
            return Results.Ok(ToRecruiterDto(profile, shared));
        }).RequireAuthorization(policy => policy.RequireRole("Recruiter"));
    }

    private static async Task<IResult> UpdateAsync(ProfileUpdateRequest input, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Trim().Length > 150 ||
            input.Headline?.Length > 250 || input.Summary?.Length > 4000 || input.Location?.Length > 250 ||
            input.ContactEmail?.Length > 254 || input.ContactPhone?.Length > 32 || input.DesiredRole?.Length > 150 ||
            input.ExperienceYears is < 0 or > 99.99m || input.RowVersion < 0)
            return Results.Problem(statusCode: 400, title: "invalid_profile");
        if (!string.IsNullOrWhiteSpace(input.ContactEmail) && !MailAddress.TryCreate(input.ContactEmail, out _))
            return Results.Problem(statusCode: 400, title: "invalid_profile");
        if (input.PreferredWorkMode is not null && !WorkModes.Contains(input.PreferredWorkMode))
            return Results.Problem(statusCode: 400, title: "invalid_profile");
        if (input.SharedFields is null || input.SharedFields.Length > SharedFieldAllowlist.Count || input.SharedFields.Any(x => !SharedFieldAllowlist.Contains(x)) || input.SharedFields.Distinct(StringComparer.Ordinal).Count() != input.SharedFields.Length)
            return Results.Problem(statusCode: 400, title: "invalid_shared_fields");

        var details = input.Details.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "{}" : input.Details.GetRawText();
        if (details.Length > 16000 || !ValidDetails(details)) return Results.Problem(statusCode: 400, title: "invalid_profile_details");
        var profile = await db.CandidateProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile is null) return Results.NotFound();
        if (profile.RowVersion != input.RowVersion) return Results.Conflict(new { title = "profile_conflict" });

        profile.DisplayName = input.DisplayName.Trim();
        profile.Headline = input.Headline?.Trim();
        profile.Summary = input.Summary?.Trim();
        profile.Location = input.Location?.Trim();
        profile.ContactEmail = input.ContactEmail?.Trim();
        profile.ContactPhone = input.ContactPhone?.Trim();
        profile.DesiredRole = input.DesiredRole?.Trim();
        profile.PreferredWorkMode = input.PreferredWorkMode;
        profile.ExperienceYears = input.ExperienceYears;
        profile.Details = details;
        profile.RowVersion++;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        var currentVisibility = await db.CandidateProfileVisibility.Where(x => x.CandidateId == userId).ToListAsync(ct);
        db.CandidateProfileVisibility.RemoveRange(currentVisibility);
        db.CandidateProfileVisibility.AddRange(input.SharedFields.Select(field => new CandidateProfileVisibility { CandidateId = userId, FieldKey = field }));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { title = "profile_conflict" }); }
        return Results.Ok(ToFullDto(profile, input.SharedFields));
    }

    private static bool ValidDetails(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.EnumerateObject().All(x => DetailAllowlist.Contains(x.Name));
        }
        catch (JsonException) { return false; }
    }

    private static object ToFullDto(CandidateProfile profile, IEnumerable<string> sharedFields) => new
    {
        userId = profile.UserId,
        rowVersion = profile.RowVersion,
        displayName = profile.DisplayName,
        headline = profile.Headline,
        summary = profile.Summary,
        location = profile.Location,
        contactEmail = profile.ContactEmail,
        contactPhone = profile.ContactPhone,
        desiredRole = profile.DesiredRole,
        preferredWorkMode = profile.PreferredWorkMode,
        experienceYears = profile.ExperienceYears,
        details = JsonDocument.Parse(profile.Details).RootElement.Clone(),
        sharedFields = sharedFields.Order(StringComparer.Ordinal).ToArray()
    };

    private static object ToRecruiterDto(CandidateProfile profile, HashSet<string> shared)
    {
        using var document = JsonDocument.Parse(profile.Details);
        var details = document.RootElement.ValueKind == JsonValueKind.Object
            ? document.RootElement.EnumerateObject().Where(x => shared.Contains(x.Name)).ToDictionary(x => x.Name, x => x.Value.Clone())
            : new Dictionary<string, JsonElement>();
        return new
        {
            displayName = shared.Contains("display_name") ? profile.DisplayName : null,
            headline = shared.Contains("headline") ? profile.Headline : null,
            summary = shared.Contains("summary") ? profile.Summary : null,
            location = shared.Contains("location") ? profile.Location : null,
            contactEmail = shared.Contains("contact_email") ? profile.ContactEmail : null,
            contactPhone = shared.Contains("contact_phone") ? profile.ContactPhone : null,
            desiredRole = shared.Contains("desired_role") ? profile.DesiredRole : null,
            preferredWorkMode = shared.Contains("preferred_work_mode") ? profile.PreferredWorkMode : null,
            experienceYears = shared.Contains("experience_years") ? profile.ExperienceYears : null,
            details
        };
    }
}
