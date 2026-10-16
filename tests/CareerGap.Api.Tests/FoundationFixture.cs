using System.Net.Http.Json;
using CareerGap.Api.Applications;
using CareerGap.Api.Ai;
using CareerGap.Api.Accounts;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset now = utcNow.ToUniversalTime();
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(TimeSpan by) => now = now.Add(by);
}

public sealed class StorageDeleteFault { public bool FailDeletes { get; set; } }

public sealed class FaultInjectingFileStorage(IFileStorage inner, StorageDeleteFault fault) : IFileStorage
{
    public string Provider => inner.Provider;
    public Task PutAsync(string key, byte[] bytes, string contentType, CancellationToken ct) => inner.PutAsync(key, bytes, contentType, ct);
    public Task<byte[]> ReadAsync(string key, CancellationToken ct) => inner.ReadAsync(key, ct);
    public Task DeleteAsync(string key, CancellationToken ct) => fault.FailDeletes
        ? Task.FromException(new IOException("synthetic storage deletion failure"))
        : inner.DeleteAsync(key, ct);
}

public sealed class TestApp(string connection, string storagePath, FakeLlmClient? fake = null, TimeProvider? clock = null,
    StorageDeleteFault? storageFault = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", connection);
        builder.UseSetting("Storage:Root", storagePath);
        builder.UseSetting("Storage:Provider", "Local");
        builder.UseSetting("Ai:Provider", "Fake");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDb>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDb>>();
            services.AddDbContext<AppDb>(o => o.UseNpgsql(connection, pg => pg.SetPostgresVersion(16, 0)));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(clock ?? TimeProvider.System);
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(sp => new FaultInjectingFileStorage(
                new LocalFileStorage(sp.GetRequiredService<IWebHostEnvironment>(), sp.GetRequiredService<IConfiguration>()),
                storageFault ?? new StorageDeleteFault()));
            if (fake is not null) { services.RemoveAll<FakeLlmClient>(); services.AddSingleton(fake); }
            foreach (var workerType in new[] { typeof(JobWorker), typeof(AccountDeletionWorker), typeof(ApplicationRetentionWorker), typeof(OrphanFileCleanupWorker) })
            {
                var worker = services.SingleOrDefault(x => x.ImplementationType == workerType);
                if (worker is not null) services.Remove(worker);
            }
        });
    }
}
public sealed class FoundationFixture : IAsyncLifetime
{
    public TestApp App { get; private set; } = null!;
    public string Connection { get; private set; } = "";
    private string adminConnection = "";
    private readonly string database = "careergap_test_" + Guid.NewGuid().ToString("N");
    public ManualTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);
    public StorageDeleteFault StorageFault { get; } = new();
    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("CAREERGAP_TEST_DATABASE") ?? throw new InvalidOperationException("Set CAREERGAP_TEST_DATABASE to a PostgreSQL 16 local admin connection; tests create/drop only a unique careergap_test_* database.");
        var cs = new NpgsqlConnectionStringBuilder(source) { Database = "postgres" };
        adminConnection = cs.ConnectionString;
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {database}", admin).ExecuteNonQueryAsync();
        cs.Database = database; Connection = cs.ConnectionString;
        App = new TestApp(Connection, Path.Combine(Path.GetTempPath(), database), clock: Clock, storageFault: StorageFault);
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal(database, db.Database.GetDbConnection().Database);
        await db.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await new NpgsqlCommand($"DROP DATABASE {database} WITH (FORCE)", admin).ExecuteNonQueryAsync();
        var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var storagePath = Path.GetFullPath(Path.Combine(tempRoot, database));
        if (storagePath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(storagePath).StartsWith("careergap_test_", StringComparison.Ordinal) && Directory.Exists(storagePath))
            Directory.Delete(storagePath, recursive: true);
    }
    public async Task<(HttpClient Client, Guid UserId)> User(string role = "Candidate", string? password = null)
    {
        using var scope = App.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
        var user = new AppUser { Id = Guid.NewGuid(), UserName = $"fake-{Guid.NewGuid():N}@example.invalid" };
        user.Email = user.UserName;
        password ??= "F!" + Guid.NewGuid().ToString("N");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.True((await users.CreateAsync(user, password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        if (role == "Candidate")
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.CandidateProfiles.Add(new CandidateProfile { UserId = user.Id }); await db.SaveChangesAsync();
        }
        var client = App.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Csrf(client);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password })).StatusCode);
        await Csrf(client);
        return (client, user.Id);
    }

    public static async Task Csrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
    public static byte[] Docx(string text = "Dữ liệu giả. Kỹ năng: React, TypeScript, PostgreSQL. Synthetic testing only.")
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(text))), new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("Docker — kiểm thử bảng và dấu tiếng Việt"))))))));
            main.Document.Save();
        }
        return stream.ToArray();
    }
    public static async Task<System.Text.Json.JsonElement> Upload(HttpClient client, byte[]? bytes = null, string name = "synthetic.docx")
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes ?? Docx()), "file", name);
        var response = await client.PostAsync("/api/resumes", content);
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
    }
    public async Task<AiJob> Enqueue(Guid uid, Guid version, Guid? key = null)
    {
        using var scope = App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<JobService>().Enqueue(uid, new(version, key ?? Guid.NewGuid()), default);
    }
}
