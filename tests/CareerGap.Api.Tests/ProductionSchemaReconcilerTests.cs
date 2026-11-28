using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

            // Simulate hybrid state where community_searches was created manually while community_searchs still existed
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
}
