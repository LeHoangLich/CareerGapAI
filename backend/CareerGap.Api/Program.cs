using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using CareerGap.Api.Accounts;
using CareerGap.Api.Ai;
using CareerGap.Api.Applications;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Integrations;
using CareerGap.Api.Matching;
using CareerGap.Api.Profiles;
using CareerGap.Api.Recruitment;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Set ConnectionStrings__Database"), pg => pg.SetPostgresVersion(16, 0)));
builder.Services.AddIdentity<AppUser, IdentityRole<Guid>>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 12;
    o.Lockout.MaxFailedAccessAttempts = 5;
}).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromSeconds(5));
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "CareerGap.Session";
    o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(2);
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
});
var dp = builder.Services.AddDataProtection().SetApplicationName("CareerGapAI").PersistKeysToDbContext<AppDb>();
if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
{
    if (builder.Configuration["Storage:Provider"] != "S3") throw new InvalidOperationException("Production requires private durable S3 storage");
    var path = builder.Configuration["DataProtection:CertificatePath"] ?? throw new InvalidOperationException("Production requires a Data Protection certificate");
    dp.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(path, builder.Configuration["DataProtection:CertificatePassword"]));
}
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "CareerGap.Csrf";
    o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Lax;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = DocumentParser.MaxBytes + 65536; o.ValueLengthLimit = 1024; o.MultipartHeadersLengthLimit = 4096; });
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = DocumentParser.MaxBytes + 65536);
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("register", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("recovery", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 4, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("matching", http => RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Environment.IsEnvironment("Testing") ? 1000 : 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSingleton<DocumentParser>();
builder.Services.AddSingleton<RedactedResumeBuilder>();
builder.Services.AddSingleton<LocalMailOutbox>();
builder.Services.AddScoped<IEmailSender, FakeEmailSender>();
builder.Services.AddSingleton<IFileStorage>(sp => builder.Configuration["Storage:Provider"] == "S3" ? new S3FileStorage(builder.Configuration) : new LocalFileStorage(sp.GetRequiredService<IWebHostEnvironment>(), builder.Configuration));
builder.Services.AddScoped<OrphanFileCleanupService>();
builder.Services.AddHostedService<OrphanFileCleanupWorker>();
builder.Services.Configure<KeywordBaselineOptions>(builder.Configuration.GetSection("Matching:Baseline"));
builder.Services.AddScoped<KeywordMatchingService>();
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
builder.Services.AddHttpClient<DeepSeekClient>(c => { c.BaseAddress = new Uri("https://api.deepseek.com/"); c.Timeout = TimeSpan.FromSeconds(45); });
builder.Services.AddSingleton<FakeLlmClient>();
builder.Services.AddScoped<ILlmClient>(sp => builder.Configuration["Ai:Provider"] == "DeepSeek" ? sp.GetRequiredService<DeepSeekClient>() : sp.GetRequiredService<FakeLlmClient>());
builder.Services.AddScoped<JobService>();
builder.Services.AddSingleton<JobWakeup>();
builder.Services.AddHostedService<JobWorker>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<ApplicationRetentionService>();
builder.Services.AddHostedService<ApplicationRetentionWorker>();
builder.Services.AddScoped<AccountDeletionService>();
builder.Services.AddHostedService<AccountDeletionWorker>();
var app = builder.Build();

if (Array.IndexOf(args, "--export-deletion-ledger") is var exportLedgerIndex && exportLedgerIndex >= 0)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Deletion ledger export is local-development only");
    var connection = new NpgsqlConnectionStringBuilder(app.Configuration.GetConnectionString("Database"));
    if (connection.Database != "careergap" || connection.Host is not ("localhost" or "127.0.0.1" or "::1"))
        throw new InvalidOperationException("Deletion ledger export requires the local careergap database");
    var destination = Path.GetFullPath(args.ElementAtOrDefault(exportLedgerIndex + 1) ?? throw new InvalidOperationException("Ledger output path required"));
    IReadOnlyList<AccountDeletionLedgerEntry> entries;
    using (var scope = app.Services.CreateScope())
        entries = await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().ExportLedgerAsync(default);
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    var temporary = destination + ".tmp";
    await File.WriteAllTextAsync(temporary, System.Text.Json.JsonSerializer.Serialize(entries));
    File.Move(temporary, destination, overwrite: true);
    Console.WriteLine($"Deletion ledger exported ({entries.Count} entries).");
    return;
}

if (Array.IndexOf(args, "--replay-deletion-ledger") is var replayLedgerIndex && replayLedgerIndex >= 0)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Deletion ledger replay is local-development only");
    var connection = new NpgsqlConnectionStringBuilder(app.Configuration.GetConnectionString("Database"));
    if (connection.Database?.StartsWith("careergap_restore_", StringComparison.Ordinal) != true ||
        connection.Host is not ("localhost" or "127.0.0.1" or "::1") || app.Configuration["Storage:Provider"] == "S3")
        throw new InvalidOperationException("Deletion ledger replay requires an isolated local restore database and local storage");
    var source = Path.GetFullPath(args.ElementAtOrDefault(replayLedgerIndex + 1) ?? throw new InvalidOperationException("Ledger input path required"));
    var entries = System.Text.Json.JsonSerializer.Deserialize<AccountDeletionLedgerEntry[]>(await File.ReadAllTextAsync(source))
        ?? throw new InvalidDataException("deletion_ledger_invalid");
    using (var scope = app.Services.CreateScope())
        await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().ReplayLedgerAsync(entries, default);
    Console.WriteLine($"Deletion ledger replay completed ({entries.Length} entries); restored user sessions were invalidated.");
    return;
}

if (Array.IndexOf(args, "--probe-file") is var probeIndex && probeIndex >= 0)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("File probes are development-only");
    var probePath = args.ElementAtOrDefault(probeIndex + 1) ?? throw new InvalidOperationException("File path required");
    try
    {
        var document = app.Services.GetRequiredService<DocumentParser>().Parse(await File.ReadAllBytesAsync(probePath), Path.GetFileName(probePath), default);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { document.MimeType, Segments = document.Segments.Count, Characters = document.Segments.Sum(x => x.Text.Length), Parser = DocumentParser.Version }));
    }
    catch (DocumentError error) { Console.WriteLine(error.Code); Environment.ExitCode = 1; }
    return;
}

if (Array.IndexOf(args, "--evaluate-keyword-baseline") is var matchingEvaluationIndex && matchingEvaluationIndex >= 0)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Matching evaluation is local-development only");
    var datasetPath = Path.GetFullPath(args.ElementAtOrDefault(matchingEvaluationIndex + 1) ?? throw new InvalidOperationException("Evaluation dataset path required"));
    var outputPath = Path.GetFullPath(args.ElementAtOrDefault(matchingEvaluationIndex + 2) ?? throw new InvalidOperationException("Evaluation output path required"));
    if (string.Equals(datasetPath, outputPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        throw new InvalidOperationException("Evaluation output must not overwrite its input dataset");
    var topKText = args.ElementAtOrDefault(matchingEvaluationIndex + 3);
    var topK = topKText is null ? (int?)null : int.TryParse(topKText, out var parsedTopK) ? parsedTopK : throw new InvalidOperationException("Invalid top-k value");
    var baseline = app.Configuration.GetSection("Matching:Baseline").Get<KeywordBaselineOptions>() ?? new KeywordBaselineOptions();
    var result = await MatchingEvaluationRunner.RunAsync(datasetPath, topK, baseline, default);
    var revision = await MatchingEvaluationRunner.ReadGitRevisionAsync();
    result = result with { SourceRevision = revision };
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    var tempPath = outputPath + ".tmp";
    await File.WriteAllTextAsync(tempPath, System.Text.Json.JsonSerializer.Serialize(result, MatchingEvaluationRunner.JsonOptions));
    File.Move(tempPath, outputPath, overwrite: true);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { result.RunId, result.DatasetId, result.Split,
        result.MethodVersion, result.ConfigurationHash, result.InputSha256, result.SourceRevision, result.SourceAssemblySha256,
        CvToJobs = result.CvToJobs.Metrics, JobsToResumes = result.JobsToResumes.Metrics }));
    return;
}

// Explicit local provisioning; no public registration or implicit production seed.
if (args.Contains("--create-demo-user"))
{
    if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing")) throw new InvalidOperationException("Demo provisioning is local/test only");
    using var scope = app.Services.CreateScope();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    var email = Environment.GetEnvironmentVariable("DEMO_EMAIL") ?? throw new InvalidOperationException("Set DEMO_EMAIL");
    var password = Environment.GetEnvironmentVariable("DEMO_PASSWORD") ?? throw new InvalidOperationException("Set DEMO_PASSWORD");
    var role = Environment.GetEnvironmentVariable("DEMO_ROLE") ?? "Candidate";
    if (role is not ("Candidate" or "Recruiter" or "Admin")) throw new InvalidOperationException("Invalid role");
    foreach (var name in new[] { "Candidate", "Recruiter", "Admin" }) if (!await roles.RoleExistsAsync(name)) await roles.CreateAsync(new IdentityRole<Guid>(name));
    var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email };
    var created = await users.CreateAsync(user, password);
    if (!created.Succeeded) throw new InvalidOperationException(string.Join(",", created.Errors.Select(x => x.Code)));
    var assigned = await users.AddToRoleAsync(user, role);
    if (!assigned.Succeeded) throw new InvalidOperationException("Role assignment failed");
    if (role == "Candidate")
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.CandidateProfiles.Add(new CandidateProfile { UserId = user.Id }); await db.SaveChangesAsync();
    }
    Console.WriteLine("Demo account created (identifier omitted).");
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (http, next) =>
{
    if (http.Request.Path.StartsWithSegments("/api"))
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.XContentTypeOptions = "nosniff";
    }
    await next();
});
app.UseAuthentication();
app.UseRateLimiter();
app.Use(async (http, next) =>
{
    if (http.User.Identity?.IsAuthenticated == true && http.Request.Path.StartsWithSegments("/api"))
    {
        var users = http.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var user = await users.GetUserAsync(http.User);
        var signin = http.RequestServices.GetRequiredService<SignInManager<AppUser>>();
        var path = http.Request.Path.Value;
        var deletionRecovery =
            (http.Request.Method == "GET" && (path == "/api/auth/csrf" || path == "/api/account/deletion-request")) ||
            (http.Request.Method == "POST" && (path == "/api/account/deletion-request/cancel" || path == "/api/auth/logout" || path == "/api/auth/login"));
        if (user?.AccountStatus == "deletion_pending")
        {
            // Preserve a restricted session for the password-confirmed cancellation path only.
            if (!deletionRecovery)
            {
                http.Response.StatusCode = await signin.ValidateSecurityStampAsync(http.User) is null ? 401 : 403;
                return;
            }
        }
        else if (user?.AccountStatus != "active" || await signin.ValidateSecurityStampAsync(http.User) is null)
        { await signin.SignOutAsync(); http.Response.StatusCode = 401; return; }
        if (http.User.Identity is ClaimsIdentity identity)
        {
            foreach (var claim in identity.FindAll(ClaimTypes.Role).ToArray()) identity.RemoveClaim(claim);
            foreach (var role in await users.GetRolesAsync(user)) identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
    }
    if (http.Request.Path.StartsWithSegments("/api") && http.Request.Method is not ("GET" or "HEAD" or "OPTIONS"))
    {
        try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); }
        catch (AntiforgeryValidationException) { await Results.Problem(statusCode: 400, title: "csrf_invalid").ExecuteAsync(http); return; }
    }
    await next();
});
app.UseAuthorization();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapAccounts(); app.MapProfiles(); app.MapCompanyJobEndpoints(); app.MapJobRevisions(); app.MapFiles(); app.MapResumeRevisions(); app.MapPublicResumeEndpoints(); app.MapJobs(); app.MapApplicationEndpoints();
app.MapKeywordMatchingEndpoints();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi();
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
