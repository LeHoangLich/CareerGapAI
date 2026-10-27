using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace CareerGap.Api.Learning;
public sealed class LearningCacheWorker(IServiceScopeFactory scopes, ILogger<LearningCacheWorker> logger) : BackgroundService
{
    public static Task<int> Expire(AppDb db, DateTimeOffset now, CancellationToken ct) => db.LearningRequests
        .Where(x => x.LinksJson != null && x.ExpiresAt <= now).ExecuteUpdateAsync(s => s.SetProperty(x => x.LinksJson, (string?)null), ct);
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); await Expire(scope.ServiceProvider.GetRequiredService<AppDb>(), DateTimeOffset.UtcNow, stoppingToken); }
            catch (Exception e) when (e is not OperationCanceledException) { logger.LogWarning("Learning metadata expiry unavailable"); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
