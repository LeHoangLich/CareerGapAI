using System.Net.Http.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class TestApp(string connection, string storagePath, FakeLlmClient? fake = null) : WebApplicationFactory<Program>
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
            if (fake is not null) { services.RemoveAll<FakeLlmClient>(); services.AddSingleton(fake); }
            var worker = services.SingleOrDefault(x => x.ImplementationType == typeof(JobWorker));
            if (worker is not null) services.Remove(worker);
        });
    }
}
public sealed class FoundationFixture : IAsyncLifetime
{
    public TestApp App { get; private set; } = null!;
    public string Connection { get; private set; } = "";
    private string adminConnection = "";
    private readonly string database = "careergap_test_" + Guid.NewGuid().ToString("N");
    public async Task InitializeAsync()
    {
        var source = Environment.GetEnvironmentVariable("CAREERGAP_TEST_DATABASE") ?? throw new InvalidOperationException("Set CAREERGAP_TEST_DATABASE to a PostgreSQL 16 local admin connection; tests create/drop only a unique careergap_test_* database.");
        var cs = new NpgsqlConnectionStringBuilder(source) { Database = "postgres" };
        adminConnection = cs.ConnectionString;
        await using var admin = new NpgsqlConnection(adminConnection); await admin.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {database}", admin).ExecuteNonQueryAsync();
        cs.Database = database; Connection = cs.ConnectionString;
        App = new TestApp(Connection, Path.Combine(Path.GetTempPath(), database));
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
    }
    public async Task<(HttpClient Client, Guid UserId)> User(string role = "Candidate")
    {
        using var scope = App.Services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync(role)) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role))).Succeeded);
        var user = new AppUser { Id = Guid.NewGuid(), UserName = $"fake-{Guid.NewGuid():N}@example.invalid" };
        user.Email = user.UserName;
        var password = "F!" + Guid.NewGuid().ToString("N");
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
