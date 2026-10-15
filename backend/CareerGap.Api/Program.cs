using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using CareerGap.Api.Accounts;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

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
});
builder.Services.AddSingleton<DocumentParser>();
builder.Services.AddSingleton<IFileStorage>(sp => builder.Configuration["Storage:Provider"] == "S3" ? new S3FileStorage(builder.Configuration) : new LocalFileStorage(sp.GetRequiredService<IWebHostEnvironment>(), builder.Configuration));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
builder.Services.AddHttpClient<DeepSeekClient>(c => { c.BaseAddress = new Uri("https://api.deepseek.com/"); c.Timeout = TimeSpan.FromSeconds(45); });
builder.Services.AddSingleton<FakeLlmClient>();
builder.Services.AddScoped<ILlmClient>(sp => builder.Configuration["Ai:Provider"] == "DeepSeek" ? sp.GetRequiredService<DeepSeekClient>() : sp.GetRequiredService<FakeLlmClient>());
builder.Services.AddScoped<JobService>();
builder.Services.AddSingleton<JobWakeup>();
builder.Services.AddHostedService<JobWorker>();
var app = builder.Build();

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
        if (user?.AccountStatus != "active" || await signin.ValidateSecurityStampAsync(http.User) is null)
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
app.MapAccounts(); app.MapFiles(); app.MapJobs();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi();
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
