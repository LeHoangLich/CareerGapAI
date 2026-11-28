using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class ProductionSchemaReconcilerTests
{
    [Fact]
    public async Task ReconcileAndSeedHandlesHybridSchemaStateAndRemainsIdempotent()
    {
        var source = Environment.GetEnvironmentVariable("CAREERGAP_TEST_DATABASE") ?? throw new InvalidOperationException("test_database_required");
        var admin = new NpgsqlConnectionStringBuilder(source) { Database = "postgres" };
        var name = "careergap_reconcile_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {name}", connection).ExecuteNonQueryAsync();
        try
        {
            admin.Database = name;
            await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(admin.ConnectionString).Options);
            await db.GetService<IMigrator>().MigrateAsync("20261120125556_StageP3DiscoveryFeatures");

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE community_searches (
                    term character varying(40) NOT NULL,
                    viewer character varying(64) NOT NULL,
                    day date NOT NULL,
                    CONSTRAINT "PK_community_searches" PRIMARY KEY (term, viewer, day)
                );
                INSERT INTO community_searchs (term, viewer, day) VALUES ('react', 'viewer1', '2026-11-27');
                """);

            await ProductionSchemaReconciler.ReconcileAndSeedAsync(db, NullLogger.Instance, seedShowcaseIfEmpty: true);
            await ProductionSchemaReconciler.ReconcileAndSeedAsync(db, NullLogger.Instance, seedShowcaseIfEmpty: true);

            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Equal(2, await db.Jobs.CountAsync(x => x.Status == "published"));
            Assert.Equal(2, await db.JobDataRevisions.CountAsync(x => x.Origin == "user_confirmed"));
            Assert.Equal(2, await db.CommunityPosts.CountAsync(x => x.Status == "published"));
            Assert.Equal(1, await db.CommunitySearches.CountAsync(x => x.Term == "react"));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", connection).ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task RestrictedDmlOnlyRuntimeRoleSeedsShowcaseAndSupportsAuthWithoutExtendedColumns()
    {
        var source = Environment.GetEnvironmentVariable("CAREERGAP_TEST_DATABASE") ?? throw new InvalidOperationException("test_database_required");
        var admin = new NpgsqlConnectionStringBuilder(source) { Database = "postgres" };
        var name = "careergap_dmlonly_" + Guid.NewGuid().ToString("N");
        var roleName = "cg_rt_" + Guid.NewGuid().ToString("N")[..8];
        var storageDir = Path.Combine(Path.GetTempPath(), "cg-dml-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageDir);
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {name}", connection).ExecuteNonQueryAsync();
        await new NpgsqlCommand($"CREATE ROLE {roleName} LOGIN PASSWORD 'rt_pass_123!'", connection).ExecuteNonQueryAsync();
        try
        {
            admin.Database = name;
            // Migrate only up to 20261126 as owner (so AspNetUsers does NOT have DisplayName/AvatarImageId/Notify* columns!)
            await using (var ownerDb = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(admin.ConnectionString).Options))
            {
                await ownerDb.GetService<IMigrator>().MigrateAsync("20261126134034_NormalizeSavedRoadmapsAndCommunitySearches");
            }
            await using (var ownerConn = new NpgsqlConnection(admin.ConnectionString))
            {
                await ownerConn.OpenAsync();
                var grantSql =
                    "GRANT CONNECT ON DATABASE " + name + " TO " + roleName + ";" +
                    "GRANT USAGE ON SCHEMA public TO " + roleName + ";" +
                    "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO " + roleName + ";" +
                    "GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO " + roleName + ";";
                await new NpgsqlCommand(grantSql, ownerConn).ExecuteNonQueryAsync();
            }

            var runtimeCs = new NpgsqlConnectionStringBuilder(admin.ConnectionString)
            {
                Username = roleName,
                Password = "rt_pass_123!"
            }.ConnectionString;

            await using var app = new TestApp(runtimeCs, storageDir);
            var schemaOptions = app.Services.GetRequiredService<AppDbSchemaOptions>();
            await ProductionSchemaReconciler.PrepareDatabaseAsync(runtimeCs, schemaOptions, app.Services, NullLogger.Instance, seedShowcaseIfEmpty: true);

            Assert.False(schemaOptions.HasExtendedUserColumns);

            using var client = app.CreateClient();
            var jobsResp = await client.GetFromJsonAsync<JsonElement>("/api/jobs?page=1&pageSize=6");
            Assert.Equal(2, jobsResp.GetProperty("total").GetInt32());

            var postsResp = await client.GetFromJsonAsync<JsonElement>("/api/community/posts?mode=discover&sort=newest&page=1");
            Assert.Equal(2, postsResp.GetProperty("total").GetInt32());

            var csrf1 = (await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf")).GetProperty("token").GetString()!;
            using var badLogin = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(new { email = "nobody@example.com", password = "WrongPassword123!" })
            };
            badLogin.Headers.Add("X-CSRF-TOKEN", csrf1);
            var badLoginResp = await client.SendAsync(badLogin);
            Assert.Equal(HttpStatusCode.Unauthorized, badLoginResp.StatusCode);

            using var regReq = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
            {
                Content = JsonContent.Create(new { email = "candidate.dml@example.com", password = "StrongPassword123!", role = "Candidate", displayName = "Ứng viên DML" })
            };
            regReq.Headers.Add("X-CSRF-TOKEN", csrf1);
            var regResp = await client.SendAsync(regReq);
            Assert.Equal(HttpStatusCode.Created, regResp.StatusCode);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", connection).ExecuteNonQueryAsync();
            await new NpgsqlCommand($"DROP ROLE IF EXISTS {roleName}", connection).ExecuteNonQueryAsync();
            try { Directory.Delete(storageDir, recursive: true); } catch { }
        }
    }
}
