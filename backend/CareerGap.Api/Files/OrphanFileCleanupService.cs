using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Files;

public sealed class OrphanFileCleanupService(AppDb db, IFileStorage storage, TimeProvider clock)
{
    internal static readonly TimeSpan GracePeriod = TimeSpan.FromHours(24);
    internal const int BatchSize = 5;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    public async Task<int> CleanupAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cutoff = now - GracePeriod;
        List<StoredFile> claimed;
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            claimed = await db.StoredFiles.FromSqlInterpolated($"""
                SELECT * FROM stored_files
                WHERE status IN ('pending','delete_pending') AND created_at <= {cutoff}
                  AND (cleanup_next_attempt_at IS NULL OR cleanup_next_attempt_at <= {now})
                  AND (cleanup_lease_until IS NULL OR cleanup_lease_until <= {now})
                  AND NOT EXISTS (SELECT 1 FROM resume_versions rv WHERE rv.file_id = stored_files.id)
                  AND NOT EXISTS (SELECT 1 FROM candidate_public_resumes cpr WHERE cpr.redacted_file_id = stored_files.id)
                ORDER BY created_at, id LIMIT {BatchSize} FOR UPDATE SKIP LOCKED
                """)
                .ToListAsync(ct);

            foreach (var file in claimed)
            {
                file.Status = "delete_pending";
                file.CleanupLeaseUntil = now + LeaseDuration;
                file.CleanupLastErrorCode = null;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        var deleted = 0;
        foreach (var claimedFile in claimed)
        {
            ct.ThrowIfCancellationRequested();
            var error = await DeleteObjectAsync(claimedFile, ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var file = await db.StoredFiles.FromSqlInterpolated(
                $"SELECT * FROM stored_files WHERE id = {claimedFile.Id} AND status = 'delete_pending' FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (file is null)
            {
                await transaction.CommitAsync(ct);
                continue;
            }

            file.CleanupLeaseUntil = null;
            file.CleanupAttemptCount++;
            if (error is null)
            {
                var stillReferenced = await db.ResumeVersions.AnyAsync(x => x.FileId == file.Id, ct) ||
                    await db.CandidatePublicResumes.AnyAsync(x => x.RedactedFileId == file.Id, ct);
                if (stillReferenced)
                {
                    // Keep the row and object protected if a concurrent or out-of-band reference appeared.
                    file.CleanupLastErrorCode = "file_still_referenced";
                    file.CleanupNextAttemptAt = clock.GetUtcNow() + TimeSpan.FromHours(1);
                }
                else
                {
                    db.StoredFiles.Remove(file);
                    await db.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    deleted++;
                    continue;
                }
            }
            else
            {
                file.CleanupLastErrorCode = error;
                var seconds = Math.Min(3600, 5 * Math.Pow(2, Math.Min(file.CleanupAttemptCount, 9)));
                file.CleanupNextAttemptAt = clock.GetUtcNow().AddSeconds(seconds);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return deleted;
    }

    private async Task<string?> DeleteObjectAsync(StoredFile file, CancellationToken ct)
    {
        if (!string.Equals(file.Provider, storage.Provider, StringComparison.Ordinal)) return "storage_provider_unavailable";
        try
        {
            // Delete is idempotent for the configured object stores; a retry handles a crash before row removal.
            await storage.DeleteAsync(file.ObjectKey, ct);
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return "storage_delete_failed"; }
    }
}

public sealed class OrphanFileCleanupWorker(IServiceScopeFactory scopes, ILogger<OrphanFileCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var cleanup = scope.ServiceProvider.GetRequiredService<OrphanFileCleanupService>();
                var deleted = await cleanup.CleanupAsync(stoppingToken);
                if (deleted > 0) logger.LogInformation("Pending file cleanup removed {FileCount} unreferenced files", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                logger.LogWarning("Pending file cleanup pass deferred ({ExceptionType})", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
