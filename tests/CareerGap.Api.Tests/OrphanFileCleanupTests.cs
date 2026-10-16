using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class OrphanFileCleanupTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task StalePendingFilesAreDeletedAfterGraceWithRetryAndReadyFilesAreKept()
    {
        var (_, ownerId) = await fixture.User();
        var key = Guid.NewGuid().ToString("N");
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.StoredFiles.Add(new StoredFile
            {
                OwnerUserId = ownerId, ObjectKey = key, Provider = "Local", OriginalName = "interrupted.docx",
                MimeType = "application/octet-stream", SizeBytes = 1, CreatedAt = fixture.Clock.GetUtcNow().AddHours(-25)
            });
            db.StoredFiles.Add(new StoredFile
            {
                OwnerUserId = ownerId, ObjectKey = Guid.NewGuid().ToString("N"), Provider = "Local",
                OriginalName = "current.docx", MimeType = "application/octet-stream", SizeBytes = 1,
                Sha256 = new string('a', 64), Status = "ready", VerifiedAt = fixture.Clock.GetUtcNow()
            });
            db.StoredFiles.Add(new StoredFile
            {
                OwnerUserId = ownerId, ObjectKey = Guid.NewGuid().ToString("N"), Provider = "Local",
                OriginalName = "recent.docx", MimeType = "application/octet-stream", SizeBytes = 1,
                CreatedAt = fixture.Clock.GetUtcNow()
            });
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IFileStorage>().PutAsync(key, [1, 2, 3], "application/octet-stream", default);
        }

        fixture.StorageFault.FailDeletes = true;
        await RunCleanup();
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var failed = await db.StoredFiles.SingleAsync(x => x.ObjectKey == key);
            Assert.Equal("delete_pending", failed.Status);
            Assert.Equal(1, failed.CleanupAttemptCount);
            Assert.Equal("storage_delete_failed", failed.CleanupLastErrorCode);
            Assert.NotNull(failed.CleanupNextAttemptAt);
        }

        fixture.StorageFault.FailDeletes = false;
        fixture.Clock.Advance(TimeSpan.FromSeconds(20));
        await RunCleanup();
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.False(await db.StoredFiles.AnyAsync(x => x.ObjectKey == key));
            Assert.Equal(1, await db.StoredFiles.CountAsync(x => x.OwnerUserId == ownerId && x.Status == "ready"));
            Assert.Equal(1, await db.StoredFiles.CountAsync(x => x.OwnerUserId == ownerId && x.Status == "pending"));
        }

        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await fixture.App.Services.GetRequiredService<IFileStorage>().ReadAsync(key, default));
    }

    private async Task RunCleanup()
    {
        using var scope = fixture.App.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OrphanFileCleanupService>().CleanupAsync(default);
    }
}
