using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerGap.Api.Accounts;

public sealed record AccountDeletionState(string Status, DateTimeOffset RequestedAt, DateTimeOffset PurgeAfter);
public sealed record AccountDeletionRequestResult(string? Error, AccountDeletionRequestRecord? Request);
public sealed record AccountDeletionCancelResult(string? Error, AppUser? User);
public sealed record AccountDeletionLedgerEntry(Guid RequestId, Guid UserId, string Status, DateTimeOffset RequestedAt,
    DateTimeOffset PurgeAfter, DateTimeOffset? CancelledAt, DateTimeOffset? DataPurgedAt, DateTimeOffset? PurgedAt);

public sealed class AccountDeletionService(
    AppDb db,
    IFileStorage storage,
    IPasswordHasher<AppUser> passwordHasher,
    TimeProvider clock,
    ILogger<AccountDeletionService> logger)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public async Task<AccountDeletionRequestResult> RequestAsync(Guid userId, string? password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 256)
            return new("current_password_invalid", null);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var account = await LockUserAsync(userId, ct);
        if (account is null) return new("account_not_found", null);

        var roles = await (from userRole in db.UserRoles
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where userRole.UserId == userId
                           select role.Name).ToArrayAsync(ct);
        if (!roles.Any(x => x is "Candidate" or "Recruiter") || roles.Contains("Admin"))
            return new("account_deletion_unavailable", null);
        if (account.AccountStatus == "deletion_pending")
        {
            var prior = await db.AccountDeletionRequests.SingleOrDefaultAsync(x => x.UserId == userId && x.Status == "pending", ct);
            if (prior is not null) { await transaction.CommitAsync(ct); return new(null, prior); }
        }
        if (account.AccountStatus != "active") return new("account_deletion_unavailable", null);
        if (account.PasswordHash is null || passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password) == PasswordVerificationResult.Failed)
            return new("current_password_invalid", null);

        var now = clock.GetUtcNow();
        var request = new AccountDeletionRequestRecord
        {
            UserId = userId,
            Status = "pending",
            RequestedAt = now,
            PurgeAfter = now.AddDays(7)
        };
        account.AccountStatus = "deletion_pending";
        account.SecurityStamp = Guid.NewGuid().ToString("N");
        account.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        db.AccountDeletionRequests.Add(request);

        // Revoke public links at T0; cancellation deliberately does not restore this selection.
        await db.CandidatePublicResumes.Where(x => x.CandidateId == userId).ExecuteDeleteAsync(ct);
        if (roles.Contains("Candidate"))
        {
            var applicationIds = await db.Applications.Where(x => x.CandidateId == userId).Select(x => x.Id).ToArrayAsync(ct);
            if (applicationIds.Length > 0)
                await db.Notifications.Where(x => x.ApplicationId != null && applicationIds.Contains(x.ApplicationId.Value)).ExecuteDeleteAsync(ct);
        }
        await CloseAndArchiveRecruiterDataAsync(userId, now, ct);
        var aiJobIds = await CancelActiveAiJobsAsync(userId, now, ct);
        if (aiJobIds.Length > 0)
            await db.Notifications.Where(x => x.AiJobId != null && aiJobIds.Contains(x.AiJobId.Value)).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(null, request);
    }

    public async Task<AccountDeletionCancelResult> CancelAsync(Guid userId, string? password, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(password) || password.Length > 256)
            return new("current_password_invalid", null);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var account = await LockUserAsync(userId, ct);
        if (account is null || account.AccountStatus != "deletion_pending") return new("deletion_not_cancellable", null);
        var request = await db.AccountDeletionRequests.FromSqlInterpolated(
            $"SELECT * FROM account_deletion_requests WHERE user_id = {userId} AND status = 'pending' FOR UPDATE").SingleOrDefaultAsync(ct);
        var now = clock.GetUtcNow();
        if (request is null || request.DataPurgedAt is not null || now >= request.PurgeAfter)
            return new("deletion_not_cancellable", null);
        if (account.PasswordHash is null || passwordHasher.VerifyHashedPassword(account, account.PasswordHash, password) == PasswordVerificationResult.Failed)
            return new("current_password_invalid", null);

        request.Status = "cancelled";
        request.CancelledAt = now;
        request.LeaseOwner = null;
        request.LeaseUntil = null;
        request.LeaseGeneration++;
        account.AccountStatus = "active";
        account.SecurityStamp = Guid.NewGuid().ToString("N");
        account.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(null, account);
    }

    public Task<AccountDeletionState?> GetStateAsync(Guid userId, CancellationToken ct) => db.AccountDeletionRequests
        .AsNoTracking().Where(x => x.UserId == userId && x.Status == "pending")
        .OrderByDescending(x => x.RequestedAt)
        .Select(x => new AccountDeletionState(x.Status, x.RequestedAt, x.PurgeAfter))
        .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AccountDeletionLedgerEntry>> ExportLedgerAsync(CancellationToken ct) =>
        await db.AccountDeletionRequests.AsNoTracking().OrderBy(x => x.RequestedAt).ThenBy(x => x.Id)
            .Select(x => new AccountDeletionLedgerEntry(x.Id, x.UserId, x.Status, x.RequestedAt, x.PurgeAfter,
                x.CancelledAt, x.DataPurgedAt, x.PurgedAt)).ToArrayAsync(ct);

    // Recovery-only operation. The caller must point at an isolated local restore database and
    // keep the API closed until this method returns successfully.
    public async Task ReplayLedgerAsync(IReadOnlyCollection<AccountDeletionLedgerEntry> entries, CancellationToken ct)
    {
        if (entries.Count > 100_000) throw new InvalidDataException("deletion_ledger_too_large");
        var ordered = entries.OrderBy(x => x.RequestedAt).ThenBy(x => x.RequestId).ToArray();
        if (ordered.Select(x => x.RequestId).Distinct().Count() != ordered.Length || ordered.Any(x =>
                x.RequestId == Guid.Empty || x.UserId == Guid.Empty || x.PurgeAfter <= x.RequestedAt ||
                x.Status is not ("pending" or "cancelled" or "purged") ||
                (x.Status == "cancelled") != (x.CancelledAt is not null) ||
                (x.Status == "purged") != (x.DataPurgedAt is not null && x.PurgedAt is not null)))
            throw new InvalidDataException("deletion_ledger_invalid");

        foreach (var entry in ordered)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var account = await LockUserAsync(entry.UserId, ct);
            var request = await db.AccountDeletionRequests.FromSqlInterpolated(
                $"SELECT * FROM account_deletion_requests WHERE id = {entry.RequestId} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (request is null)
            {
                if (account is null)
                {
                    // The restored snapshot predates this account; it contains no related rows to revive.
                    await transaction.CommitAsync(ct);
                    continue;
                }
                // Re-purged entries are replayed as pending so the same, reviewed purge path erases
                // data from an older snapshot instead of merely restoring its tombstone.
                request = new AccountDeletionRequestRecord
                {
                    Id = entry.RequestId,
                    UserId = entry.UserId,
                    RequestedAt = entry.RequestedAt,
                    PurgeAfter = entry.PurgeAfter,
                    Status = entry.Status == "cancelled" ? "cancelled" : "pending",
                    CancelledAt = entry.Status == "cancelled" ? entry.CancelledAt : null
                };
                db.AccountDeletionRequests.Add(request);
            }
            else
            {
                if (request.UserId != entry.UserId || request.RequestedAt != entry.RequestedAt || request.PurgeAfter != entry.PurgeAfter)
                    throw new InvalidDataException("deletion_ledger_conflict");
                if (entry.Status == "cancelled" && request.Status == "pending" && request.DataPurgedAt is null)
                {
                    request.Status = "cancelled";
                    request.CancelledAt = entry.CancelledAt;
                    request.LeaseOwner = null;
                    request.LeaseUntil = null;
                    request.LeaseGeneration++;
                }
                else if (entry.Status == "purged" && request.Status == "cancelled")
                    throw new InvalidDataException("deletion_ledger_conflict");
                else if (entry.Status == "pending" && request.Status is "cancelled" or "purged")
                    throw new InvalidDataException("deletion_ledger_conflict");
            }

            if (account is not null)
            {
                if (entry.Status == "cancelled")
                {
                    if (account.AccountStatus == "deleted") throw new InvalidDataException("deletion_ledger_conflict");
                    account.AccountStatus = "active";
                }
                else if (account.AccountStatus != "deleted") account.AccountStatus = "deletion_pending";
                account.SecurityStamp = Guid.NewGuid().ToString("N");
                account.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        var latestByUser = ordered.GroupBy(x => x.UserId).Select(x => x.OrderBy(y => y.RequestedAt).ThenBy(y => y.RequestId).Last()).ToArray();
        foreach (var entry in latestByUser.Where(x => x.Status != "cancelled" && x.PurgeAfter <= clock.GetUtcNow()))
        {
            var workerId = $"restore-{Guid.NewGuid():N}";
            var attempts = 0;
            while (await ProcessOneDueAsync(workerId, ct) && ++attempts < 10_000) { }
            if (attempts >= 10_000) throw new InvalidOperationException("restore_deletion_replay_incomplete");
            if (await db.AccountDeletionRequests.AnyAsync(x => x.Id == entry.RequestId && x.Status == "pending", ct) ||
                await db.AccountDeletionObjects.AnyAsync(x => x.RequestId == entry.RequestId && x.Status == "pending", ct))
                throw new InvalidOperationException("restore_deletion_replay_incomplete");
        }

        // A database restore must never revive browser sessions issued before the recovery point.
        var users = await db.Users.ToListAsync(ct);
        foreach (var user in users)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ProcessOneDueAsync(string workerId, CancellationToken ct)
    {
        var claim = await ClaimAsync(workerId, ct);
        if (claim is null) return false;

        try
        {
            if (!await PurgeDatabaseRowsAsync(claim.Value, ct)) return true;
            var objects = await db.AccountDeletionObjects.AsNoTracking()
                .Where(x => x.RequestId == claim.Value.Id && x.Status == "pending")
                .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
            foreach (var item in objects)
            {
                var error = await DeleteObjectAsync(item, ct);
                await SaveObjectAttemptAsync(claim.Value, item.Id, error, ct);
            }
            await CompleteOrRetryAsync(claim.Value, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogWarning("Account deletion processing deferred ({ExceptionType})", ex.GetType().Name);
            await SaveRequestFailureAsync(claim.Value, PurgeErrorCode(ex), ct);
        }
        return true;
    }

    private async Task<(Guid Id, string Owner, long Generation)?> ClaimAsync(string workerId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var requests = await db.AccountDeletionRequests.FromSqlInterpolated(
            $"SELECT * FROM account_deletion_requests WHERE status = 'pending' AND purge_after <= {now} AND (lease_until IS NULL OR lease_until <= {now}) ORDER BY purge_after, requested_at, id LIMIT 1 FOR UPDATE SKIP LOCKED")
            .ToListAsync(ct);
        var request = requests.SingleOrDefault();
        if (request is null) { await transaction.CommitAsync(ct); return null; }
        request.LeaseOwner = workerId;
        request.LeaseGeneration++;
        request.LeaseUntil = now.Add(LeaseDuration);
        request.AttemptCount++;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (request.Id, workerId, request.LeaseGeneration);
    }

    private async Task<bool> PurgeDatabaseRowsAsync((Guid Id, string Owner, long Generation) claim, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var request = await db.AccountDeletionRequests.FromSqlInterpolated(
            $"SELECT * FROM account_deletion_requests WHERE id = {claim.Id} FOR UPDATE").SingleAsync(ct);
        if (!OwnsLease(request, claim, clock.GetUtcNow())) { await transaction.CommitAsync(ct); return false; }
        if (request.DataPurgedAt is not null) { await transaction.CommitAsync(ct); return true; }

        var account = await LockUserAsync(request.UserId, ct) ?? throw new InvalidOperationException("deletion_user_missing");
        if (account.AccountStatus is not ("deletion_pending" or "deleted"))
            throw new InvalidOperationException("deletion_account_state_conflict");
        var roles = await (from userRole in db.UserRoles
                           join role in db.Roles on userRole.RoleId equals role.Id
                           where userRole.UserId == account.Id
                           select role.Name).ToArrayAsync(ct);
        var isCandidate = roles.Contains("Candidate");
        var isRecruiter = roles.Contains("Recruiter");

        await db.Database.ExecuteSqlRawAsync("SET LOCAL careergap.account_purge = 'on'", ct);

        var aiJobIds = await db.AiJobs.Where(x => x.RequestedBy == account.Id).Select(x => x.Id).ToArrayAsync(ct);
        var applicationIds = isCandidate
            ? await db.Applications.Where(x => x.CandidateId == account.Id).Select(x => x.Id).ToArrayAsync(ct)
            : [];

        await db.Notifications.Where(x => x.RecipientUserId == account.Id ||
            (applicationIds.Length > 0 && x.ApplicationId != null && applicationIds.Contains(x.ApplicationId.Value)) ||
            (aiJobIds.Length > 0 && x.AiJobId != null && aiJobIds.Contains(x.AiJobId.Value)))
            .ExecuteDeleteAsync(ct);

        if (applicationIds.Length > 0)
        {
            await db.ApplicationNotes.Where(x => applicationIds.Contains(x.ApplicationId)).ExecuteDeleteAsync(ct);
            await db.ApplicationStatusHistory.Where(x => applicationIds.Contains(x.ApplicationId)).ExecuteDeleteAsync(ct);
            await db.Applications.Where(x => applicationIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }
        if (isRecruiter)
        {
            // Internal notes may contain recruiter identity or other private text. Preserve other users' applications/history.
            await db.ApplicationNotes.Where(x => x.AuthorUserId == account.Id).ExecuteDeleteAsync(ct);
        }

        await db.SavedJobs.Where(x => x.CandidateId == account.Id).ExecuteDeleteAsync(ct);
        await db.JobCandidateBlocks.Where(x => x.CandidateId == account.Id || x.CreatedByUserId == account.Id).ExecuteDeleteAsync(ct);
        await db.CandidatePublicResumes.Where(x => x.CandidateId == account.Id).ExecuteDeleteAsync(ct);
        await db.CandidateProfileVisibility.Where(x => x.CandidateId == account.Id).ExecuteDeleteAsync(ct);
        await db.ResumeDataRevisions.Where(x => x.CandidateId == account.Id).ExecuteDeleteAsync(ct);
        await db.AiJobs.Where(x => x.RequestedBy == account.Id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.ResumeVersionId, (Guid?)null)
            .SetProperty(x => x.OutputJson, (string?)null)
            .SetProperty(x => x.InputHash, ""), ct);

        if (isCandidate)
        {
            await db.ResumeVersions.Where(x => x.CandidateId == account.Id).ExecuteDeleteAsync(ct);
            await db.Resumes.Where(x => x.CandidateId == account.Id).ExecuteDeleteAsync(ct);
        }
        if (isRecruiter)
        {
            var companies = await db.Companies.Where(x => x.OwnerUserId == account.Id).ToListAsync(ct);
            var now = clock.GetUtcNow();
            foreach (var company in companies)
            {
                company.Name = "Nhà tuyển dụng đã xóa";
                company.Slug = $"deleted-{account.Id:N}-{company.Id:N}";
                company.Description = null;
                company.WebsiteUrl = null;
                company.Location = null;
                company.Industry = null;
                company.SizeLabel = null;
                company.ArchivedAt ??= now;
                company.UpdatedAt = now;
                company.RowVersion++;
            }
        }

        await DeleteFilesToObjectLedgerAsync(request.Id, account.Id, ct);

        await db.UserClaims.Where(x => x.UserId == account.Id).ExecuteDeleteAsync(ct);
        await db.UserLogins.Where(x => x.UserId == account.Id).ExecuteDeleteAsync(ct);
        await db.UserTokens.Where(x => x.UserId == account.Id).ExecuteDeleteAsync(ct);
        await db.UserRoles.Where(x => x.UserId == account.Id).ExecuteDeleteAsync(ct);

        if (isCandidate)
        {
            var profile = await db.CandidateProfiles.SingleOrDefaultAsync(x => x.UserId == account.Id, ct);
            if (profile is not null)
            {
                profile.DisplayName = "Ứng viên đã xóa";
                profile.Headline = null;
                profile.Summary = null;
                profile.Location = null;
                profile.ContactEmail = null;
                profile.ContactPhone = null;
                profile.DesiredRole = null;
                profile.PreferredWorkMode = null;
                profile.ExperienceYears = null;
                profile.Details = "{}";
                profile.RowVersion++;
                profile.UpdatedAt = clock.GetUtcNow();
            }
        }

        AnonymizeAccount(account);
        account.AccountStatus = "deleted";
        var completedAt = clock.GetUtcNow();
        request.DataPurgedAt = completedAt;
        request.LastErrorCode = null;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private async Task DeleteFilesToObjectLedgerAsync(Guid requestId, Guid userId, CancellationToken ct)
    {
        var files = await db.StoredFiles.Where(x => x.OwnerUserId == userId).ToArrayAsync(ct);
        foreach (var file in files)
        {
            var referencedByResume = await db.ResumeVersions.AnyAsync(x => x.FileId == file.Id, ct);
            var referencedByPublication = await db.CandidatePublicResumes.AnyAsync(x => x.RedactedFileId == file.Id, ct);
            if (referencedByResume || referencedByPublication)
                throw new InvalidOperationException("file_still_referenced");
            db.AccountDeletionObjects.Add(new AccountDeletionObjectRecord
            {
                RequestId = requestId,
                Provider = file.Provider,
                ObjectKey = file.ObjectKey,
                Status = "pending",
                CreatedAt = clock.GetUtcNow()
            });
            db.StoredFiles.Remove(file);
        }
    }

    private async Task<string?> DeleteObjectAsync(AccountDeletionObjectRecord item, CancellationToken ct)
    {
        if (!string.Equals(item.Provider, storage.Provider, StringComparison.Ordinal)) return "storage_provider_unavailable";
        try
        {
            await storage.DeleteAsync(item.ObjectKey, ct);
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return "storage_delete_failed";
        }
    }

    private async Task SaveObjectAttemptAsync((Guid Id, string Owner, long Generation) claim, Guid objectId, string? error, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var request = await db.AccountDeletionRequests.FromSqlInterpolated(
            $"SELECT * FROM account_deletion_requests WHERE id = {claim.Id} FOR UPDATE").SingleAsync(ct);
        if (!OwnsLease(request, claim, clock.GetUtcNow())) { await transaction.CommitAsync(ct); return; }
        var item = await db.AccountDeletionObjects.SingleAsync(x => x.Id == objectId && x.RequestId == claim.Id, ct);
        item.AttemptCount++;
        item.LastErrorCode = error;
        if (error is null)
        {
            item.Status = "deleted";
            item.DeletedAt = clock.GetUtcNow();
        }
        request.LastErrorCode = error;
        request.LeaseUntil = clock.GetUtcNow().Add(LeaseDuration);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task CompleteOrRetryAsync((Guid Id, string Owner, long Generation) claim, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var request = await db.AccountDeletionRequests.FromSqlInterpolated(
            $"SELECT * FROM account_deletion_requests WHERE id = {claim.Id} FOR UPDATE").SingleAsync(ct);
        if (!OwnsLease(request, claim, clock.GetUtcNow())) { await transaction.CommitAsync(ct); return; }
        var pending = await db.AccountDeletionObjects.AsNoTracking().Where(x => x.RequestId == request.Id && x.Status == "pending").ToListAsync(ct);
        if (pending.Count == 0)
        {
            request.Status = "purged";
            request.PurgedAt = clock.GetUtcNow();
            request.LastErrorCode = null;
            request.LeaseOwner = null;
            request.LeaseUntil = null;
        }
        else
        {
            request.LastErrorCode = pending.Select(x => x.LastErrorCode).FirstOrDefault(x => x is not null) ?? "storage_delete_failed";
            ReleaseForRetry(request);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task SaveRequestFailureAsync((Guid Id, string Owner, long Generation) claim, string error, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var request = await db.AccountDeletionRequests.FromSqlInterpolated(
            $"SELECT * FROM account_deletion_requests WHERE id = {claim.Id} FOR UPDATE").SingleOrDefaultAsync(ct);
        if (request is null || !OwnsLease(request, claim, clock.GetUtcNow())) { await transaction.CommitAsync(ct); return; }
        request.LastErrorCode = error;
        ReleaseForRetry(request);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private void ReleaseForRetry(AccountDeletionRequestRecord request)
    {
        request.LeaseOwner = null;
        var seconds = Math.Min(3600, 5 * Math.Pow(2, Math.Min(request.AttemptCount, 9)));
        request.LeaseUntil = clock.GetUtcNow().AddSeconds(seconds);
    }

    private static string PurgeErrorCode(Exception exception)
    {
        if (exception is InvalidOperationException { Message: "file_still_referenced" }) return "file_still_referenced";
        if (exception is InvalidOperationException { Message: "deletion_user_missing" }) return "deletion_user_missing";
        if (exception is InvalidOperationException { Message: "deletion_account_state_conflict" }) return "deletion_account_state_conflict";
        var databaseError = FindPostgresException(exception);
        if (databaseError is not null)
        {
            var identifiers = new[] { databaseError.SqlState, databaseError.TableName, databaseError.ConstraintName }
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var code = $"database_{string.Join('_', identifiers)}";
            return code[..Math.Min(80, code.Length)];
        }
        return "purge_failed";
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is PostgresException postgres) return postgres;
        return null;
    }

    private async Task CloseAndArchiveRecruiterDataAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var companyIds = await db.Companies.Where(x => x.OwnerUserId == userId).Select(x => x.Id).ToArrayAsync(ct);
        if (companyIds.Length == 0) return;
        await db.Companies.Where(x => x.OwnerUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ArchivedAt, x => x.ArchivedAt ?? now)
                .SetProperty(x => x.UpdatedAt, now).SetProperty(x => x.RowVersion, x => x.RowVersion + 1), ct);
        await db.Jobs.Where(x => companyIds.Contains(x.CompanyId) && x.Status == "published")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "closed")
                .SetProperty(x => x.ClosedAt, now).SetProperty(x => x.UpdatedAt, now)
                .SetProperty(x => x.RowVersion, x => x.RowVersion + 1), ct);
    }

    private async Task<Guid[]> CancelActiveAiJobsAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var jobs = await db.AiJobs.FromSqlInterpolated(
            $"SELECT * FROM ai_jobs WHERE requested_by = {userId} ORDER BY id FOR UPDATE").ToListAsync(ct);
        foreach (var job in jobs)
        {
            if (job.Status is "queued" or "running")
            {
                job.Status = "cancelled";
                job.ErrorCode = "access_revoked";
                job.FinishedAt = now;
                job.LeaseGeneration++;
                job.LeaseOwner = null;
                job.LeaseUntil = null;
            }

            var reservation = await db.BudgetReservations.FromSqlInterpolated(
                $"SELECT * FROM budget_reservations WHERE ai_job_id = {job.Id} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (reservation is null || reservation.UnallocatedAmount <= 0) continue;
            var budget = await db.BudgetPeriods.FromSqlInterpolated(
                $"SELECT * FROM budget_periods WHERE id = {reservation.BudgetPeriodId} FOR UPDATE").SingleAsync(ct);
            budget.ReservedAmount -= reservation.UnallocatedAmount;
            reservation.ReservedRemaining -= reservation.UnallocatedAmount;
            reservation.UnallocatedAmount = 0;
        }
        return jobs.Select(x => x.Id).ToArray();
    }

    private static void AnonymizeAccount(AppUser account)
    {
        var tombstoneEmail = $"deleted-{account.Id:N}@deleted.invalid";
        account.UserName = tombstoneEmail;
        account.NormalizedUserName = tombstoneEmail.ToUpperInvariant();
        account.Email = tombstoneEmail;
        account.NormalizedEmail = tombstoneEmail.ToUpperInvariant();
        account.EmailConfirmed = false;
        account.PasswordHash = null;
        account.SecurityStamp = Guid.NewGuid().ToString("N");
        account.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        account.PhoneNumber = null;
        account.PhoneNumberConfirmed = false;
        account.TwoFactorEnabled = false;
        account.LockoutEnabled = false;
        account.LockoutEnd = null;
        account.AccessFailedCount = 0;
    }

    private static bool OwnsLease(AccountDeletionRequestRecord request, (Guid Id, string Owner, long Generation) claim, DateTimeOffset now) =>
        request.Status == "pending" && request.LeaseOwner == claim.Owner && request.LeaseGeneration == claim.Generation && request.LeaseUntil > now;

    private async Task<AppUser?> LockUserAsync(Guid id, CancellationToken ct) => await db.Users.FromSqlInterpolated(
        $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync(ct);
}

public sealed class AccountDeletionWorker(IServiceScopeFactory scopes, ILogger<AccountDeletionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = Guid.NewGuid().ToString("N");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
                if (!await service.ProcessOneDueAsync(workerId, stoppingToken))
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                logger.LogWarning("Account deletion worker unavailable ({ExceptionType})", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
