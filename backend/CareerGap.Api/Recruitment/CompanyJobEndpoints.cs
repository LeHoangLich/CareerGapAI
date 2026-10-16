using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Recruitment;

public sealed record CompanyCreateRequest(string? Name, string? Description, string? WebsiteUrl, string? Location, string? Industry, string? SizeLabel);
public sealed record CompanyUpdateRequest(long RowVersion, string? Name, string? Description, string? WebsiteUrl, string? Location, string? Industry, string? SizeLabel);
public sealed record JobContentRequest(
    string? Title, string? Description, string? RequirementsText, string? BenefitsText,
    string? Industry, string? Location, string? EmploymentType, string? WorkMode,
    decimal? ExperienceMin, decimal? ExperienceMax, decimal? SalaryMin, decimal? SalaryMax,
    string? SalaryCurrency, string? SalaryPeriod, string? Language);
public sealed record JobCreateRequest(Guid CompanyId, DateTimeOffset? ApplicationDeadline, JobContentRequest? Content);
public sealed record JobUpdateRequest(long RowVersion, DateTimeOffset? ApplicationDeadline, JobContentRequest? Content);
public sealed record JobStateRequest(long RowVersion);
public sealed record JobVersionDto(Guid Id, int VersionNo, string Title, string Description, string RequirementsText, string? BenefitsText,
    string? Industry, string? Location, string EmploymentType, string WorkMode, decimal? ExperienceMin, decimal? ExperienceMax,
    decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency, string? SalaryPeriod, string Language, string ContentHash, DateTimeOffset CreatedAt);
public sealed record CompanyDto(Guid Id, string Name, string Slug, string? Description, string? WebsiteUrl, string? Location,
    string? Industry, string? SizeLabel, long RowVersion, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record JobDto(Guid Id, string Status, Guid CompanyId, CompanyDto Company, JobVersionDto Version,
    DateTimeOffset? PublishedAt, DateTimeOffset? ClosedAt, DateTimeOffset? ApplicationDeadline, bool AcceptingApplications,
    long RowVersion, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record JobPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public static class CompanyJobEndpoints
{
    private static readonly HashSet<string> EmploymentTypes = new(StringComparer.Ordinal)
        { "full_time", "part_time", "contract", "internship", "temporary" };
    private static readonly HashSet<string> WorkModes = new(StringComparer.Ordinal)
        { "onsite", "hybrid", "remote", "unspecified" };
    private static readonly HashSet<string> SalaryPeriods = new(StringComparer.Ordinal)
        { "hour", "day", "month", "year", "project" };
    private static readonly HashSet<string> Languages = new(StringComparer.Ordinal)
        { "vi", "en", "vi-en" };

    public static void MapCompanyJobEndpoints(this WebApplication app)
    {
        var recruiters = app.MapGroup("/api").RequireAuthorization(policy => policy.RequireRole("Recruiter"));
        recruiters.MapGet("/companies/mine", GetCompaniesAsync);
        recruiters.MapGet("/companies/{id:guid}", GetCompanyAsync);
        recruiters.MapPost("/companies", CreateCompanyAsync);
        recruiters.MapPut("/companies/{id:guid}", UpdateCompanyAsync);
        recruiters.MapGet("/jobs/mine", GetMyJobsAsync);
        recruiters.MapPost("/jobs", CreateJobAsync);
        recruiters.MapPut("/jobs/{id:guid}", UpdateJobAsync);
        recruiters.MapPost("/jobs/{id:guid}/publish", PublishJobAsync);
        recruiters.MapPost("/jobs/{id:guid}/close", CloseJobAsync);
        recruiters.MapGet("/jobs/{id:guid}/versions", GetJobVersionsAsync);

        app.MapGet("/api/jobs", SearchPublicJobsAsync);
        app.MapGet("/api/jobs/{id:guid}", GetPublicJobAsync);

        var candidates = app.MapGroup("/api/saved-jobs").RequireAuthorization(policy => policy.RequireRole("Candidate"));
        candidates.MapGet("", GetSavedJobsAsync);
        candidates.MapPut("/{jobId:guid}", SaveJobAsync);
        candidates.MapDelete("/{jobId:guid}", UnsaveJobAsync);
    }

    private static async Task<IResult> GetCompaniesAsync(ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        var companies = await db.Companies.AsNoTracking().Where(x => x.OwnerUserId == ownerId && x.ArchivedAt == null)
            .OrderBy(x => x.Name).ToListAsync(ct);
        return Results.Ok(companies.Select(ToCompanyDto).ToArray());
    }

    private static async Task<IResult> GetCompanyAsync(Guid id, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == ownerId && x.ArchivedAt == null, ct);
        return company is null ? Results.NotFound() : Results.Ok(ToCompanyDto(company));
    }

    private static async Task<IResult> CreateCompanyAsync(CompanyCreateRequest input, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        var name = Clean(input.Name);
        var description = Clean(input.Description);
        var websiteUrl = Clean(input.WebsiteUrl);
        var location = Clean(input.Location);
        var industry = Clean(input.Industry);
        var sizeLabel = Clean(input.SizeLabel);
        var validation = ValidateCompany(name, description, websiteUrl, location, industry, sizeLabel);
        if (validation is not null) return Results.Problem(statusCode: 400, title: validation);
        var now = DateTimeOffset.UtcNow;
        var companyId = Guid.NewGuid();
        var company = new Company { Id = companyId, OwnerUserId = ownerId, Name = name!, Slug = MakeSlug(name!, companyId),
            Description = description, WebsiteUrl = websiteUrl, Location = location,
            Industry = industry, SizeLabel = sizeLabel, CreatedAt = now, UpdatedAt = now };
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/companies/{company.Id}", ToCompanyDto(company));
    }

    private static async Task<IResult> UpdateCompanyAsync(Guid id, CompanyUpdateRequest input, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        var name = Clean(input.Name);
        var description = Clean(input.Description);
        var websiteUrl = Clean(input.WebsiteUrl);
        var location = Clean(input.Location);
        var industry = Clean(input.Industry);
        var sizeLabel = Clean(input.SizeLabel);
        var validation = ValidateCompany(name, description, websiteUrl, location, industry, sizeLabel);
        if (validation is not null || input.RowVersion < 0) return Results.Problem(statusCode: 400, title: validation ?? "invalid_company");
        var company = await db.Companies.SingleOrDefaultAsync(x => x.Id == id && x.OwnerUserId == ownerId && x.ArchivedAt == null, ct);
        if (company is null) return Results.NotFound();
        if (company.RowVersion != input.RowVersion) return Results.Conflict(new { title = "company_conflict" });
        company.Name = name!;
        company.Description = description;
        company.WebsiteUrl = websiteUrl;
        company.Location = location;
        company.Industry = industry;
        company.SizeLabel = sizeLabel;
        company.RowVersion++;
        company.UpdatedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { title = "company_conflict" }); }
        return Results.Ok(ToCompanyDto(company));
    }

    private static async Task<IResult> CreateJobAsync(JobCreateRequest input, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        var content = Normalize(input.Content);
        var validation = ValidateJobContent(content);
        if (validation is not null) return Results.Problem(statusCode: 400, title: validation);
        var company = await db.Companies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.CompanyId && x.OwnerUserId == ownerId && x.ArchivedAt == null, ct);
        if (company is null) return Results.NotFound();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var job = new JobPosting { CompanyId = company.Id, Status = "draft", ApplicationDeadline = input.ApplicationDeadline, CreatedAt = now, UpdatedAt = now };
        db.Jobs.Add(job);
        await db.SaveChangesAsync(ct);
        var version = CreateVersion(job.Id, ownerId, 1, content!, now);
        db.JobVersions.Add(version);
        await db.SaveChangesAsync(ct);
        job.CurrentVersionId = version.Id;
        job.RowVersion++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        job.Company = company;
        job.CurrentVersion = version;
        return Results.Created($"/api/jobs/{job.Id}", ToJobDto(job));
    }

    private static async Task<IResult> UpdateJobAsync(Guid id, JobUpdateRequest input, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        var content = Normalize(input.Content);
        var validation = ValidateJobContent(content);
        if (validation is not null || input.RowVersion < 0) return Results.Problem(statusCode: 400, title: validation ?? "invalid_job");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id = {id} FOR UPDATE", ct);
        var job = await db.Jobs.Include(x => x.Company).Include(x => x.CurrentVersion)
            .SingleOrDefaultAsync(x => x.Id == id && x.Company!.OwnerUserId == ownerId, ct);
        if (job is null) return Results.NotFound();
        if (job.Status is "closed" or "archived" || job.Company!.ArchivedAt is not null) return Results.Conflict(new { title = "job_not_editable" });
        if (job.RowVersion != input.RowVersion) return Results.Conflict(new { title = "job_conflict" });
        var now = DateTimeOffset.UtcNow;
        var version = CreateVersion(job.Id, ownerId, (job.CurrentVersion?.VersionNo ?? 0) + 1, content!, now);
        db.JobVersions.Add(version);
        await db.SaveChangesAsync(ct);
        job.CurrentVersionId = version.Id;
        job.ApplicationDeadline = input.ApplicationDeadline;
        job.RowVersion++;
        job.UpdatedAt = now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { title = "job_conflict" }); }
        await transaction.CommitAsync(ct);
        job.CurrentVersion = version;
        return Results.Ok(ToJobDto(job));
    }

    private static async Task<IResult> PublishJobAsync(Guid id, JobStateRequest input, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id = {id} FOR UPDATE", ct);
        var job = await db.Jobs.Include(x => x.Company).Include(x => x.CurrentVersion)
            .SingleOrDefaultAsync(x => x.Id == id && x.Company!.OwnerUserId == ownerId, ct);
        if (job is null) return Results.NotFound();
        if (job.RowVersion != input.RowVersion) return Results.Conflict(new { title = "job_conflict" });
        if (job.Status != "draft" || job.CurrentVersion is null || job.Company!.ArchivedAt is not null)
            return Results.Conflict(new { title = "job_not_publishable" });
        var now = DateTimeOffset.UtcNow;
        if (job.ApplicationDeadline is { } deadline && deadline <= now) return Results.Problem(statusCode: 400, title: "deadline_in_past");
        job.Status = "published";
        job.PublishedAt = now;
        job.RowVersion++;
        job.UpdatedAt = now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { title = "job_conflict" }); }
        await transaction.CommitAsync(ct);
        return Results.Ok(ToJobDto(job));
    }

    private static async Task<IResult> CloseJobAsync(Guid id, JobStateRequest input, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM jobs WHERE id = {id} FOR UPDATE", ct);
        var job = await db.Jobs.Include(x => x.Company).Include(x => x.CurrentVersion)
            .SingleOrDefaultAsync(x => x.Id == id && x.Company!.OwnerUserId == ownerId, ct);
        if (job is null) return Results.NotFound();
        if (job.RowVersion != input.RowVersion) return Results.Conflict(new { title = "job_conflict" });
        if (job.Status != "published") return Results.Conflict(new { title = "job_not_closable" });
        var now = DateTimeOffset.UtcNow;
        job.Status = "closed";
        job.ClosedAt = now;
        job.RowVersion++;
        job.UpdatedAt = now;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Results.Conflict(new { title = "job_conflict" }); }
        await transaction.CommitAsync(ct);
        return Results.Ok(ToJobDto(job));
    }

    private static async Task<IResult> SearchPublicJobsAsync(string? q, string? skill, string? industry, string? location,
        string? workMode, string? employmentType, decimal? experienceYears, int? page, int? pageSize, AppDb db, CancellationToken ct)
    {
        if (!ValidPage(page, pageSize, out var currentPage, out var size) || q?.Length > 120 || skill?.Length > 100 ||
            industry?.Length > 100 || location?.Length > 250 ||
            (workMode is not null && !WorkModes.Contains(workMode)) ||
            (employmentType is not null && !EmploymentTypes.Contains(employmentType)) || experienceYears is < 0 or > 99.99m)
            return Results.Problem(statusCode: 400, title: "invalid_job_search");
        var query = db.Jobs.AsNoTracking().Include(x => x.Company).Include(x => x.CurrentVersion)
            .Where(x => x.Status == "published" && x.Company!.ArchivedAt == null &&
                db.Users.Any(owner => owner.Id == x.Company.OwnerUserId && owner.AccountStatus == "active"));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = LikePattern(q.Trim());
            query = query.Where(x => EF.Functions.ILike(x.CurrentVersion!.Title, pattern, "\\") ||
                EF.Functions.ILike(x.CurrentVersion.Description, pattern, "\\") ||
                EF.Functions.ILike(x.CurrentVersion.RequirementsText, pattern, "\\") ||
                EF.Functions.ILike(x.Company!.Name, pattern, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(skill))
        {
            var pattern = LikePattern(skill.Trim());
            query = query.Where(x => EF.Functions.ILike(x.CurrentVersion!.RequirementsText, pattern, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(industry))
        {
            var pattern = LikePattern(industry.Trim());
            query = query.Where(x => x.CurrentVersion!.Industry != null && EF.Functions.ILike(x.CurrentVersion.Industry, pattern, "\\"));
        }
        if (!string.IsNullOrWhiteSpace(location))
        {
            var pattern = LikePattern(location.Trim());
            query = query.Where(x => (x.CurrentVersion!.Location != null && EF.Functions.ILike(x.CurrentVersion.Location, pattern, "\\")) ||
                (x.Company!.Location != null && EF.Functions.ILike(x.Company.Location, pattern, "\\")));
        }
        if (workMode is not null) query = query.Where(x => x.CurrentVersion!.WorkMode == workMode);
        if (employmentType is not null) query = query.Where(x => x.CurrentVersion!.EmploymentType == employmentType);
        if (experienceYears is { } years)
            query = query.Where(x => (x.CurrentVersion!.ExperienceMin == null || x.CurrentVersion.ExperienceMin <= years) &&
                (x.CurrentVersion.ExperienceMax == null || x.CurrentVersion.ExperienceMax >= years));
        var total = await query.CountAsync(ct);
        var jobs = await query.OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id)
            .Skip((currentPage - 1) * size).Take(size).ToListAsync(ct);
        return Results.Ok(new JobPage<JobDto>(jobs.Select(ToJobDto).ToArray(), currentPage, size, total));
    }

    private static async Task<IResult> GetPublicJobAsync(Guid id, AppDb db, CancellationToken ct)
    {
        var job = await db.Jobs.AsNoTracking().Include(x => x.Company).Include(x => x.CurrentVersion)
            .SingleOrDefaultAsync(x => x.Id == id && (x.Status == "published" || x.Status == "closed") && x.Company!.ArchivedAt == null &&
                db.Users.Any(owner => owner.Id == x.Company.OwnerUserId && owner.AccountStatus == "active"), ct);
        return job is null || job.CurrentVersion is null ? Results.NotFound() : Results.Ok(ToJobDto(job));
    }

    private static async Task<IResult> GetMyJobsAsync(ClaimsPrincipal principal, int? page, int? pageSize, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        if (!ValidPage(page, pageSize, out var currentPage, out var size)) return Results.Problem(statusCode: 400, title: "invalid_pagination");
        var query = db.Jobs.AsNoTracking().Include(x => x.Company).Include(x => x.CurrentVersion)
            .Where(x => x.Company!.OwnerUserId == ownerId && x.Company.ArchivedAt == null);
        var total = await query.CountAsync(ct);
        var jobs = await query.OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Skip((currentPage - 1) * size).Take(size).ToListAsync(ct);
        return Results.Ok(new JobPage<JobDto>(jobs.Where(x => x.CurrentVersion is not null).Select(ToJobDto).ToArray(), currentPage, size, total));
    }

    private static async Task<IResult> GetJobVersionsAsync(Guid id, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var ownerId)) return Results.Unauthorized();
        var ownsJob = await db.Jobs.AsNoTracking().AnyAsync(x => x.Id == id && x.Company!.OwnerUserId == ownerId, ct);
        if (!ownsJob) return Results.NotFound();
        var versions = await db.JobVersions.AsNoTracking().Where(x => x.JobId == id).OrderByDescending(x => x.VersionNo)
            .Select(x => new JobVersionDto(x.Id, x.VersionNo, x.Title, x.Description, x.RequirementsText, x.BenefitsText, x.Industry,
                x.Location, x.EmploymentType, x.WorkMode, x.ExperienceMin, x.ExperienceMax, x.SalaryMin, x.SalaryMax, x.SalaryCurrency,
                x.SalaryPeriod, x.Language, x.ContentHash, x.CreatedAt)).ToListAsync(ct);
        return Results.Ok(versions);
    }

    private static async Task<IResult> GetSavedJobsAsync(ClaimsPrincipal principal, int? page, int? pageSize, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var candidateId)) return Results.Unauthorized();
        if (!ValidPage(page, pageSize, out var currentPage, out var size)) return Results.Problem(statusCode: 400, title: "invalid_pagination");
        var query = from saved in db.SavedJobs.AsNoTracking()
                    join job in db.Jobs.AsNoTracking().Include(x => x.Company).Include(x => x.CurrentVersion) on saved.JobId equals job.Id
                    where saved.CandidateId == candidateId && (job.Status == "published" || job.Status == "closed") && job.Company!.ArchivedAt == null
                    orderby saved.CreatedAt descending
                    select new { saved.CreatedAt, Job = job };
        var total = await query.CountAsync(ct);
        var entries = await query.Skip((currentPage - 1) * size).Take(size).ToListAsync(ct);
        var items = entries.Where(x => x.Job.CurrentVersion is not null).Select(x => new { savedAt = x.CreatedAt, job = ToJobDto(x.Job) }).ToArray();
        return Results.Ok(new JobPage<object>(items, currentPage, size, total));
    }

    private static async Task<IResult> SaveJobAsync(Guid jobId, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var candidateId)) return Results.Unauthorized();
        var jobExists = await db.Jobs.AsNoTracking().AnyAsync(x => x.Id == jobId && (x.Status == "published" || x.Status == "closed"), ct);
        if (!jobExists) return Results.NotFound();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO saved_jobs (candidate_id, job_id, created_at) VALUES ({candidateId}, {jobId}, {DateTimeOffset.UtcNow}) ON CONFLICT (candidate_id, job_id) DO NOTHING", ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UnsaveJobAsync(Guid jobId, ClaimsPrincipal principal, AppDb db, CancellationToken ct)
    {
        if (!TryUserId(principal, out var candidateId)) return Results.Unauthorized();
        await db.SavedJobs.Where(x => x.CandidateId == candidateId && x.JobId == jobId).ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }

    private static JobVersion CreateVersion(Guid jobId, Guid ownerId, int versionNo, JobContentRequest content, DateTimeOffset now) => new()
    {
        JobId = jobId, CreatedBy = ownerId, VersionNo = versionNo, Title = content.Title!, Description = content.Description!,
        RequirementsText = content.RequirementsText!, BenefitsText = content.BenefitsText, Industry = content.Industry,
        Location = content.Location, EmploymentType = content.EmploymentType!, WorkMode = content.WorkMode!,
        ExperienceMin = content.ExperienceMin, ExperienceMax = content.ExperienceMax, SalaryMin = content.SalaryMin,
        SalaryMax = content.SalaryMax, SalaryCurrency = content.SalaryCurrency, SalaryPeriod = content.SalaryPeriod,
        Language = content.Language!, ContentHash = HashContent(content), CreatedAt = now
    };

    private static JobContentRequest? Normalize(JobContentRequest? content) => content is null ? null : new JobContentRequest(
        Clean(content.Title), Clean(content.Description), Clean(content.RequirementsText), Clean(content.BenefitsText),
        Clean(content.Industry), Clean(content.Location), Clean(content.EmploymentType), Clean(content.WorkMode),
        content.ExperienceMin, content.ExperienceMax, content.SalaryMin, content.SalaryMax,
        Clean(content.SalaryCurrency)?.ToUpperInvariant(), Clean(content.SalaryPeriod), Clean(content.Language));

    private static string? ValidateCompany(string? name, string? description, string? website, string? location, string? industry, string? sizeLabel)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || description?.Length > 4000 || location?.Length > 250 || industry?.Length > 100 || sizeLabel?.Length > 50)
            return "invalid_company";
        if (website is not null && (website.Length > 2048 || !Uri.TryCreate(website, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            return "invalid_company_website";
        return null;
    }

    private static string? ValidateJobContent(JobContentRequest? content)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.Title) || content.Title.Length > 200 ||
            string.IsNullOrWhiteSpace(content.Description) || content.Description.Length > 12000 ||
            string.IsNullOrWhiteSpace(content.RequirementsText) || content.RequirementsText.Length > 12000 ||
            content.BenefitsText?.Length > 5000 || content.Industry?.Length > 100 || content.Location?.Length > 250 ||
            content.EmploymentType is null || !EmploymentTypes.Contains(content.EmploymentType) ||
            content.WorkMode is null || !WorkModes.Contains(content.WorkMode) || content.Language is null || !Languages.Contains(content.Language) ||
            content.ExperienceMin is < 0 or > 99.99m || content.ExperienceMax is < 0 or > 99.99m ||
            (content.ExperienceMin is not null && content.ExperienceMax is not null && content.ExperienceMin > content.ExperienceMax) ||
            content.SalaryMin is < 0 or > 9999999999999999.99m || content.SalaryMax is < 0 or > 9999999999999999.99m ||
            (content.SalaryMin is not null && content.SalaryMax is not null && content.SalaryMin > content.SalaryMax))
            return "invalid_job_content";
        var salaryHasAmount = content.SalaryMin is not null || content.SalaryMax is not null;
        if (salaryHasAmount && (content.SalaryCurrency is null || content.SalaryCurrency.Length != 3 || !content.SalaryCurrency.All(char.IsAsciiLetterUpper) ||
            content.SalaryPeriod is null || !SalaryPeriods.Contains(content.SalaryPeriod))) return "invalid_job_salary";
        if (!salaryHasAmount && (content.SalaryCurrency is not null || content.SalaryPeriod is not null)) return "invalid_job_salary";
        return null;
    }

    private static CompanyDto ToCompanyDto(Company company) => new(company.Id, company.Name, company.Slug, company.Description,
        company.WebsiteUrl, company.Location, company.Industry, company.SizeLabel, company.RowVersion, company.CreatedAt, company.UpdatedAt);

    private static JobDto ToJobDto(JobPosting job)
    {
        if (job.Company is null || job.CurrentVersion is null) throw new InvalidOperationException("Published job is missing company or version.");
        var now = DateTimeOffset.UtcNow;
        var accepting = job.Status == "published" && (job.ApplicationDeadline is null || job.ApplicationDeadline > now);
        var version = job.CurrentVersion;
        var versionDto = new JobVersionDto(version.Id, version.VersionNo, version.Title, version.Description, version.RequirementsText,
            version.BenefitsText, version.Industry, version.Location, version.EmploymentType, version.WorkMode,
            version.ExperienceMin, version.ExperienceMax, version.SalaryMin, version.SalaryMax, version.SalaryCurrency,
            version.SalaryPeriod, version.Language, version.ContentHash, version.CreatedAt);
        return new JobDto(job.Id, job.Status, job.CompanyId, ToCompanyDto(job.Company), versionDto,
            job.PublishedAt, job.ClosedAt, job.ApplicationDeadline, accepting, job.RowVersion, job.CreatedAt, job.UpdatedAt);
    }

    private static string HashContent(JobContentRequest content) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(content)));
    private static string LikePattern(string value) => "%" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool TryUserId(ClaimsPrincipal principal, out Guid userId) => Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    private static bool ValidPage(int? page, int? pageSize, out int currentPage, out int size)
    {
        currentPage = page ?? 1; size = pageSize ?? 20;
        return currentPage is >= 1 and <= 1000000 && size is >= 1 and <= 50;
    }

    private static string MakeSlug(string name, Guid id)
    {
        var normalized = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        var pendingDash = false;
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(character)) { if (pendingDash && builder.Length > 0) builder.Append('-'); builder.Append(char.ToLowerInvariant(character)); pendingDash = false; }
            else pendingDash = true;
        }
        var prefix = builder.Length == 0 ? "company" : builder.ToString();
        return $"{prefix}-{id.ToString("N")[..8]}";
    }
}
