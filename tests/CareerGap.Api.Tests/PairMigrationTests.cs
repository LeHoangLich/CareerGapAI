using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class PairMigrationTests
{
    [Fact] public async Task MigrationUpgradesExistingExtractionJobAndRollsBackInIsolatedDatabase()
    {
        var source = Environment.GetEnvironmentVariable("CAREERGAP_TEST_DATABASE") ?? throw new InvalidOperationException("test_database_required");
        var admin = new NpgsqlConnectionStringBuilder(source) { Database = "postgres" };
        var name = "careergap_test_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(admin.ConnectionString); await connection.OpenAsync();
        await new NpgsqlCommand($"CREATE DATABASE {name}", connection).ExecuteNonQueryAsync();
        try
        {
            admin.Database = name;
            await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(admin.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261015211227_JobDataRevisions";
            await migrator.MigrateAsync(previous);
            var user = Guid.NewGuid(); var method = Guid.NewGuid(); var job = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"AspNetUsers\" (\"Id\",\"AccountStatus\",\"CreatedAt\",\"EmailConfirmed\",\"PhoneNumberConfirmed\",\"TwoFactorEnabled\",\"LockoutEnabled\",\"AccessFailedCount\") VALUES ({user},'active',now(),false,false,false,false,0)");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ai_method_versions (id,config_hash,provider,model,prompt_version,schema_version,parameters,created_at) VALUES ({method},'legacy-test','Fake','fake','legacy','legacy','{{}}'::jsonb,now())");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ai_jobs (id,requested_by,method_version_id,idempotency_key,input_hash,status,attempt_count,max_attempts,lease_generation,available_at,created_at) VALUES ({job},{user},{method},{Guid.NewGuid()},'legacy-input','queued',0,2,0,now(),now())");
            await migrator.MigrateAsync();
            var old = await db.AiJobs.AsNoTracking().SingleAsync(x => x.Id == job);
            Assert.Equal("extract_cv", old.JobType); Assert.Equal("legacy-input", old.InputHash);
            Assert.Equal(16, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.False(await db.MatchInputs.AnyAsync()); Assert.False(await db.MatchResults.AnyAsync());
            Assert.False(db.Database.HasPendingModelChanges());
            await migrator.MigrateAsync(previous);
            Assert.Equal(15, (await db.Database.GetAppliedMigrationsAsync()).Count());
            await migrator.MigrateAsync();
            Assert.Equal("legacy-input", (await db.AiJobs.AsNoTracking().SingleAsync(x => x.Id == job)).InputHash);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", connection).ExecuteNonQueryAsync();
        }
    }
}
