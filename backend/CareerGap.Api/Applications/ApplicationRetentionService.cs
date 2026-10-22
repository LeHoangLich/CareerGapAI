using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Applications;

public sealed class ApplicationRetentionService(AppDb db, TimeProvider clock)
{
    internal const int BatchSize = 100;

    public async Task<int> PurgeExpiredAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().AddDays(-90);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var expired = await db.Applications.FromSqlInterpolated(
            $"SELECT * FROM applications WHERE status IN ('rejected','withdrawn') AND status_changed_at <= {cutoff} ORDER BY status_changed_at, id LIMIT {BatchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(ct);
        if (expired.Count == 0)
        {
            await transaction.CommitAsync(ct);
            return 0;
        }

        var ids = expired.Select(x => x.Id).ToArray();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL careergap.application_retention = 'on'", ct);
        await Matching.MatchRetention.Erase(db, db.MatchInputs.Where(x => x.ApplicationId != null && ids.Contains(x.ApplicationId.Value)), ct);
        await db.Notifications.Where(x => x.ApplicationId != null && ids.Contains(x.ApplicationId.Value)).ExecuteDeleteAsync(ct);
        await db.ApplicationNotes.Where(x => ids.Contains(x.ApplicationId)).ExecuteDeleteAsync(ct);
        await db.ApplicationStatusHistory.Where(x => ids.Contains(x.ApplicationId)).ExecuteDeleteAsync(ct);
        await db.Applications.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return expired.Count;
    }
}

public sealed class ApplicationRetentionWorker(IServiceScopeFactory scopes, ILogger<ApplicationRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var retention = scope.ServiceProvider.GetRequiredService<ApplicationRetentionService>();
                var total = 0;
                int batch;
                do
                {
                    batch = await retention.PurgeExpiredAsync(stoppingToken);
                    total += batch;
                } while (batch == ApplicationRetentionService.BatchSize && !stoppingToken.IsCancellationRequested);
                if (total > 0) logger.LogInformation("Application retention purged {ApplicationCount} expired records", total);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                logger.LogWarning("Application retention pass deferred ({ExceptionType})", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
