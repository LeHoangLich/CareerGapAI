using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareerGap.Api.Ai;

public sealed class JobError(string code, int status) : Exception(code) { public int Status { get; } = status; public string Code { get; } = code; }
public sealed record CreateJobRequest(Guid ResumeVersionId, Guid IdempotencyKey, string Contract = "skills-v1");
public sealed class JobService(AppDb db, IOptions<AiOptions> options, IConfiguration config, JobWakeup wakeup)
{
    private const string PromptVersion = "extract-probe-v2";
    private const string OutputSchemaVersion = "skills-evidence-v2";
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    // A tracking query acquires the SQL lock but does not refresh an entity already
    // tracked by this scope. Reload while holding the lock across worker transactions
    // and batch admissions, or a concurrent reservation/settlement can be overwritten.
    private async Task<AiJob> LockJob(Guid id, CancellationToken ct)
    {
        var row = await db.AiJobs.FromSqlInterpolated($"SELECT * FROM ai_jobs WHERE id = {id} FOR UPDATE").SingleAsync(ct);
        await db.Entry(row).ReloadAsync(ct); return row;
    }
    private async Task<BudgetReservation> LockReservation(Guid jobId, CancellationToken ct)
    {
        var row = await db.BudgetReservations.FromSqlInterpolated($"SELECT * FROM budget_reservations WHERE ai_job_id = {jobId} FOR UPDATE").SingleAsync(ct);
        await db.Entry(row).ReloadAsync(ct); return row;
    }
    private async Task<BudgetPeriod> LockBudget(Guid id, CancellationToken ct)
    {
        var row = await db.BudgetPeriods.FromSqlInterpolated($"SELECT * FROM budget_periods WHERE id = {id} FOR UPDATE").SingleAsync(ct);
        await db.Entry(row).ReloadAsync(ct); return row;
    }
    private Task<DateTimeOffset> Now(CancellationToken ct) => db.Database.SqlQueryRaw<DateTimeOffset>("SELECT now() AS \"Value\"").SingleAsync(ct);

    public Task<AiJob> Enqueue(Guid uid, CreateJobRequest input, CancellationToken ct) => EnqueueCore(uid, input, null, ct);
    public Task<AiJob> EnqueueMatch(Guid uid, Guid key, MatchInput match, CancellationToken ct) =>
        EnqueueCore(uid, new(match.ResumeVersionId, key, PairMatchingContract.Version), match, ct);
    private async Task<AiJob> EnqueueCore(Guid uid, CreateJobRequest input, MatchInput? match, CancellationToken ct)
    {
        if (input.IdempotencyKey == Guid.Empty) throw new JobError("idempotency_key_required", 400);
        if (match is null && input.Contract is not ("skills-v1" or FullCvExtraction.Contract)) throw new JobError("invalid_extraction_contract", 400);
        var opt = options.Value;
        if (!opt.Enabled) throw new JobError("ai_disabled", 503);
        if (opt.Provider is not ("Fake" or "DeepSeek")) throw new JobError("provider_not_configured", 503);
        if (opt.Provider == "DeepSeek" && (!opt.RealIsConfigured() || string.IsNullOrWhiteSpace(config["Ai:ApiKey"]))) throw new JobError("provider_not_configured", 503);
        if (opt.Provider == "DeepSeek" && (match is null ? !opt.SupportsContract(input.Contract) : opt.MatchMaxOutputTokens is not (>= 128 and <= 2048)))
            throw new JobError(match is null ? "full_cv_output_budget_required" : "match_output_budget_required", 503);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize only short admission transactions: method/period creation and idempotency.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(341017)", ct);
        var version = await db.ResumeVersions.SingleOrDefaultAsync(x => x.Id == input.ResumeVersionId && x.CandidateId == (match == null ? uid : match.CandidateId), ct) ?? throw new JobError("not_found", 404);
        if (match is not null && !await PairMatchingService.CanAccess(db, uid, match, DateTimeOffset.UtcNow, ct)) throw new JobError("not_found", 404);
        var file = await db.StoredFiles.SingleAsync(x => x.Id == version.FileId && x.OwnerUserId == version.CandidateId, ct);
        if (file.Status != "ready" || file.Sha256 != version.DocumentHash) throw new JobError("file_unavailable", 409);
        var existing = await db.AiJobs.SingleOrDefaultAsync(x => x.RequestedBy == uid && x.IdempotencyKey == input.IdempotencyKey, ct);
        // Request identity excludes mutable provider configuration. A retry returns its original method.
        var inputHash = match is null ? Hash($"{PromptVersion}:{version.Id}:{version.DocumentHash}" + (input.Contract == "skills-v1" ? "" : ":" + input.Contract)) :
            Hash($"{match.Fingerprint}:{match.Context}:{match.ApplicationId}");
        if (existing is not null)
        {
            if (existing.InputHash != inputHash) throw new JobError("idempotency_conflict", 409);
            await tx.CommitAsync(ct); return existing;
        }
        var parameters = opt.Provider == "Fake" ? new MethodParameters("fake-keyword-v1", 16000, 512, 0, 0, "not-applicable") : opt.Snapshot(input.Contract);
        if (match is not null) parameters = parameters with { MaxOutputTokens = opt.Provider == "Fake" ? 2048 : opt.MatchMaxOutputTokens!.Value };
        parameters = parameters with { Contract = input.Contract, CompletionPolicy = CompletionValidator.Version, ParserVersion = DocumentParser.Version };
        var schema = match is not null ? PairMatchingContract.Version : input.Contract == FullCvExtraction.Contract ? FullCvExtraction.Contract : OutputSchemaVersion;
        var prompt = match is not null ? PairMatchingContract.Prompt + PairMatchingContract.Rubric : input.Contract == FullCvExtraction.Contract ? FullCvExtraction.Prompt : DeepSeekClient.Prompt;
        var raw = JsonSerializer.Serialize(parameters, OutputValidator.Json);
        var configHash = Hash($"{opt.Provider}:{PromptVersion}:{schema}:{OutputValidator.Version}:{Files.DocumentParser.Version}:{prompt}:{raw}");
        var method = await db.AiMethods.SingleOrDefaultAsync(x => x.ConfigHash == configHash, ct);
        if (method is null)
        {
            method = new AiMethod { ConfigHash = configHash, Provider = opt.Provider, Model = parameters.Model,
                    PromptVersion = match is null ? PromptVersion : PairMatchingContract.Version, SchemaVersion = schema, Parameters = raw };
            db.AiMethods.Add(method);
        }
        if (match is not null)
        {
            var cached = await (from j in db.AiJobs join m in db.MatchInputs on j.Id equals m.AiJobId
                where j.RequestedBy == uid && j.MethodVersionId == method.Id && j.InputHash == inputHash &&
                    j.Status == "succeeded"
                orderby j.CreatedAt descending select j).FirstOrDefaultAsync(ct);
            if (cached is not null)
            {
                // Bind every idempotency key even on a cache hit. No provider reservation or call.
                var result = await db.MatchResults.AsNoTracking().SingleAsync(x => x.AiJobId == cached.Id, ct);
                var reused = new AiJob { JobType = "match_pair", RequestedBy = uid, MethodVersionId = method.Id,
                    IdempotencyKey = input.IdempotencyKey, InputHash = inputHash, Status = "succeeded", OutputJson = cached.OutputJson,
                    FinishedAt = await Now(ct) };
                match.AiJobId = reused.Id;
                db.AiJobs.Add(reused); db.MatchInputs.Add(match);
                db.MatchResults.Add(new MatchResult { AiJobId = reused.Id, Score = result.Score, Criteria = result.Criteria, CreatedAt = result.CreatedAt });
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return reused;
            }
            if (Encoding.UTF8.GetByteCount(match.Payload + PairMatchingContract.Prompt) + 1024 > parameters.MaxInputTokens)
                throw new JobError("matching_input_limit", 422);
        }
        var now = await Now(ct);
        var start = opt.Provider == "Fake" ? new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero) : DateTimeOffset.Parse(opt.PeriodStart).ToUniversalTime();
        var end = opt.Provider == "Fake" ? start.AddMonths(1) : DateTimeOffset.Parse(opt.PeriodEnd).ToUniversalTime();
        var budget = await db.BudgetPeriods.SingleOrDefaultAsync(x => x.Service == opt.Provider && x.PeriodStart == start, ct);
        if (budget is null)
        {
            if (await db.BudgetPeriods.AnyAsync(x => x.Service == opt.Provider && x.PeriodStart < end && x.PeriodEnd > start, ct)) throw new JobError("budget_period_overlap", 409);
            budget = new BudgetPeriod { Service = opt.Provider, Unit = opt.Provider == "Fake" ? "test_credit" : "usd", PeriodStart = start, PeriodEnd = end, LimitAmount = opt.Provider == "Fake" ? 1000 : opt.MonthlyLimit };
            db.BudgetPeriods.Add(budget); await db.SaveChangesAsync(ct);
        }
        budget = await LockBudget(budget.Id, ct);
        if (budget.PeriodEnd != end) throw new JobError("budget_period_conflict", 409);
        // Decreasing the configured cap takes effect immediately; raising an existing cap is an explicit maintenance action.
        var effectiveLimit = opt.Provider == "Fake" ? budget.LimitAmount : Math.Min(budget.LimitAmount, opt.MonthlyLimit);
        var perCall = opt.Provider == "Fake" ? 1m : decimal.Ceiling((parameters.MaxInputTokens * parameters.InputPrice + parameters.MaxOutputTokens * parameters.OutputPrice) / 1000000m * 100000000m) / 100000000m;
        var estimate = perCall * 2;
        if (budget.SpentAmount + budget.ReservedAmount + estimate > effectiveLimit) throw new JobError("quota_exhausted", 429);
        await CheckUserQuota(uid, budget.Id, opt, estimate, ct);
        var job = new AiJob { JobType = match is null ? "extract_cv" : "match_pair", RequestedBy = uid, ResumeVersionId = match is null ? version.Id : null, MethodVersionId = method.Id, IdempotencyKey = input.IdempotencyKey, InputHash = inputHash, AvailableAt = now };
        db.AiJobs.Add(job);
        if (match is not null) { match.AiJobId = job.Id; db.MatchInputs.Add(match); }
        db.BudgetReservations.Add(new BudgetReservation { AiJobId = job.Id, BudgetPeriodId = budget.Id, ReservedRemaining = estimate, UnallocatedAmount = estimate, PerCallEstimate = perCall });
        budget.ReservedAmount += estimate;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        wakeup.Signal();
        return job;
    }

    private async Task CheckUserQuota(Guid uid, Guid period, AiOptions opt, decimal estimate, CancellationToken ct)
    {
        if (await db.AiJobs.CountAsync(x => x.RequestedBy == uid && (x.Status == "queued" || x.Status == "running"), ct) >= Math.Clamp(opt.UserMaxActiveJobs, 1, 10))
            throw new JobError("user_concurrency_limit", 429);
        var reservations = await (from r in db.BudgetReservations join j in db.AiJobs on r.AiJobId equals j.Id
            where j.RequestedBy == uid && r.BudgetPeriodId == period select new { r.AiJobId, r.ReservedRemaining }).ToListAsync(ct);
        var ids = reservations.Select(x => x.AiJobId).ToArray();
        var spent = await db.ExternalCallAttempts.Where(x => ids.Contains(x.AiJobId)).SumAsync(x => x.ActualAmount ?? 0m, ct);
        var limit = opt.Provider == "Fake" ? opt.FakeUserTestCreditLimit : Math.Min(opt.UserPeriodLimit, opt.MonthlyLimit);
        if (limit <= 0 || spent + reservations.Sum(x => x.ReservedRemaining) + estimate > limit) throw new JobError("user_quota_exhausted", 429);
    }

    public async Task<AiJob?> Claim(string owner, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var candidates = await db.AiJobs.FromSqlRaw("SELECT * FROM ai_jobs WHERE (status = 'queued' AND available_at <= now()) OR (status = 'running' AND lease_until < now()) ORDER BY created_at LIMIT 1 FOR UPDATE SKIP LOCKED").ToListAsync(ct);
        var job = candidates.SingleOrDefault();
        if (job is null) { await tx.CommitAsync(ct); return null; }
        // A reclaimed in-flight request may have cost money, even when there is no response.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE external_call_attempts SET status = 'outcome_unknown' WHERE ai_job_id = {job.Id} AND status = 'in_flight' AND reconciled_at IS NULL", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE call_budget_allocations SET status = 'outcome_unknown' WHERE call_attempt_id IN (SELECT id FROM external_call_attempts WHERE ai_job_id = {job.Id} AND status = 'outcome_unknown') AND status = 'allocated'", ct);
        if (job.AttemptCount >= job.MaxAttempts)
        {
            job.Status = "failed"; job.ErrorCode = "attempts_exhausted"; job.FinishedAt = await Now(ct);
            await ReleaseUnused(job.Id, ct); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return null;
        }
        job.Status = "running"; job.LeaseOwner = owner; job.LeaseGeneration++; job.AttemptCount++;
        job.LeaseUntil = (await Now(ct)).AddSeconds(Math.Clamp(options.Value.LeaseSeconds, 120, 300));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return job;
    }

    private static bool HasLease(AiJob job, string owner, long generation, DateTimeOffset now) => job.Status == "running" && job.LeaseOwner == owner && job.LeaseGeneration == generation && job.LeaseUntil > now;

    public async Task<Guid?> StartAttempt(Guid id, string owner, long generation, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await LockJob(id, ct);
        var now = await Now(ct);
        if (!HasLease(job, owner, generation, now)) return null;
        var reservation = await LockReservation(id, ct);
        var budget = await LockBudget(reservation.BudgetPeriodId, ct);
        if (now >= budget.PeriodEnd || !options.Value.Enabled) throw new ProviderError(now >= budget.PeriodEnd ? "budget_period_expired" : "ai_disabled", false);
        var active = await db.Users.AnyAsync(x => x.Id == job.RequestedBy && x.AccountStatus == "active", ct);
        if (!active) throw new ProviderError("access_revoked", false);
        if (job.JobType == "match_pair")
        {
            var input = await db.MatchInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == id, ct);
            if (input is null || !await PairMatchingService.CanAccess(db, job.RequestedBy, input, now, ct)) throw new ProviderError("access_revoked", false);
        }
        var method = await db.AiMethods.AsNoTracking().SingleAsync(x => x.Id == job.MethodVersionId, ct);
        if (method.Provider == "DeepSeek" && (!options.Value.RealIsConfigured() || options.Value.Provider != "DeepSeek")) throw new ProviderError("provider_not_configured", false);
        if (reservation.UnallocatedAmount < reservation.PerCallEstimate) throw new ProviderError("quota_exhausted", false);
        var attempt = new ExternalCallAttempt { AiJobId = id, CallNo = job.AttemptCount, LeaseGeneration = generation, Status = "in_flight", DispatchedAt = now, PricingSnapshot = method.Parameters };
        db.ExternalCallAttempts.Add(attempt);
        db.CallBudgetAllocations.Add(new CallBudgetAllocation { CallAttemptId = attempt.Id, BudgetReservationId = reservation.Id, ReservedAmount = reservation.PerCallEstimate });
        reservation.UnallocatedAmount -= reservation.PerCallEstimate;
        job.LeaseUntil = now.AddSeconds(Math.Clamp(options.Value.LeaseSeconds, 120, 300));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return attempt.Id;
    }

    // Cost reconciliation deliberately does not require a live lease. A stale worker still owes usage.
    public async Task Settle(Guid attemptId, LlmReply? reply, bool success, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var attempt = await db.ExternalCallAttempts.FromSqlInterpolated($"SELECT * FROM external_call_attempts WHERE id = {attemptId} FOR UPDATE").SingleAsync(ct);
        await db.Entry(attempt).ReloadAsync(ct);
        if (attempt.ReconciledAt is not null) { await tx.CommitAsync(ct); return; }
        var reservation = await LockReservation(attempt.AiJobId, ct);
        var budget = await LockBudget(reservation.BudgetPeriodId, ct);
        var allocation = await db.CallBudgetAllocations.SingleAsync(x => x.CallAttemptId == attemptId, ct);
        var p = JsonSerializer.Deserialize<MethodParameters>(attempt.PricingSnapshot, OutputValidator.Json)!;
        if (reply?.InputTokens is not { } input || reply.OutputTokens is not { } output)
        {
            attempt.Status = "outcome_unknown"; allocation.Status = "outcome_unknown";
        }
        else
        {
            var amount = budget.Service == "Fake" ? 1m : decimal.Round((input * p.InputPrice + output * p.OutputPrice) / 1000000m, 8, MidpointRounding.AwayFromZero);
            attempt.InputTokens = input; attempt.OutputTokens = output; attempt.ActualAmount = amount;
            attempt.ReconciledAt = await Now(ct); attempt.Status = success ? "succeeded" : "failed";
            budget.SpentAmount += amount; budget.ReservedAmount -= allocation.ReservedAmount;
            reservation.ReservedRemaining -= allocation.ReservedAmount;
            allocation.ActualAmount = amount; allocation.Status = "settled";
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private async Task ReleaseUnused(Guid id, CancellationToken ct)
    {
        var reservation = await LockReservation(id, ct);
        var budget = await LockBudget(reservation.BudgetPeriodId, ct);
        budget.ReservedAmount -= reservation.UnallocatedAmount;
        reservation.ReservedRemaining -= reservation.UnallocatedAmount;
        reservation.UnallocatedAmount = 0;
    }
    public async Task<bool> Finish(Guid id, string owner, long generation, string? output, string? error, bool retry,
        CancellationToken ct, ParsedDocument? sourceDocument = null, SkillOutput? structuredOutput = null, FullCvOutput? fullOutput = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await LockJob(id, ct);
        var now = await Now(ct);
        if (!HasLease(job, owner, generation, now)) return false;
        var active = await db.Users.AnyAsync(x => x.Id == job.RequestedBy && x.AccountStatus == "active", ct);
        if (!active) { output = null; error = "access_revoked"; retry = false; }
        if (output is not null && job.JobType == "match_pair")
        {
            var input = await db.MatchInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == id, ct);
            if (input is null || !await PairMatchingService.CanAccess(db, job.RequestedBy, input, now, ct))
            { output = null; error = "access_revoked"; retry = false; }
            else
            {
                var payload = JsonSerializer.Deserialize<PairPayload>(input.Payload, OutputValidator.Json)!;
                var score = PairMatchingContract.Validate(output, payload);
                db.MatchResults.Add(new MatchResult { AiJobId = id, Score = score.Score, Criteria = PairMatchingContract.Serialize(score.Criteria), CreatedAt = now });
            }
        }
        else if (output is not null)
        {
            if (job.ResumeVersionId is not { } versionId || sourceDocument is null || (structuredOutput is null && fullOutput is null))
            { output = null; error = "revision_input_missing"; retry = false; }
            else
            {
                var version = await db.ResumeVersions.FromSqlInterpolated(
                    $"SELECT * FROM resume_versions WHERE id = {versionId} AND candidate_id = {job.RequestedBy} FOR UPDATE")
                    .AsNoTracking().SingleOrDefaultAsync(ct);
                if (version is null) { output = null; error = "access_revoked"; retry = false; }
                else
                {
                    var parent = await db.ResumeDataRevisions.Where(x => x.ResumeVersionId == versionId)
                        .OrderByDescending(x => x.RevisionNo).FirstOrDefaultAsync(ct);
                    if (parent?.RevisionNo == int.MaxValue) { output = null; error = "resume_revision_limit"; retry = false; }
                    else
                    {
                        db.ResumeDataRevisions.Add(fullOutput is not null
                            ? FullCvExtraction.FromAi(job.RequestedBy, versionId, parent?.Id, (parent?.RevisionNo ?? 0) + 1, job.MethodVersionId, job.Id, sourceDocument, fullOutput, now)
                            : ResumeRevisionFactory.FromAi(job.RequestedBy, versionId, parent?.Id, (parent?.RevisionNo ?? 0) + 1, job.MethodVersionId, job.Id, sourceDocument, structuredOutput!, now));
                    }
                }
            }
        }
        if (retry && job.AttemptCount < job.MaxAttempts)
        { job.Status = "queued"; job.AvailableAt = now.AddSeconds(5); job.ErrorCode = error; }
        else
        {
            job.Status = output is not null ? "succeeded" : "failed"; job.OutputJson = output; job.ErrorCode = error; job.FinishedAt = now;
            await ReleaseUnused(id, ct);
        }
        job.LeaseUntil = null; job.LeaseOwner = null;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
}

public static class JobEndpoints
{
    public static void MapJobs(this WebApplication app)
    {
        app.MapPost("/api/ai/jobs", async (CreateJobRequest input, ClaimsPrincipal user, JobService service, CancellationToken ct) =>
        {
            if (!user.IsInRole("Candidate")) return Results.Forbid();
            try
            {
                var job = await service.Enqueue(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), input, ct);
                return Results.Accepted($"/api/ai/jobs/{job.Id}", new { job.Id, job.Status });
            }
            catch (JobError ex) { return Results.Problem(statusCode: ex.Status, title: ex.Code); }
        }).RequireAuthorization();
        // Explicit private export for offline evaluation. Always the original job output, never a confirmed child.
        app.MapGet("/api/ai/jobs/{id:guid}/evaluation-capture", async (Guid id, ClaimsPrincipal user, AppDb db, CancellationToken ct) =>
        {
            if (!user.IsInRole("Candidate")) return Results.Forbid();
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var row = await (from j in db.AiJobs.AsNoTracking()
                join m in db.AiMethods.AsNoTracking() on j.MethodVersionId equals m.Id
                join v in db.ResumeVersions.AsNoTracking() on j.ResumeVersionId equals v.Id
                join f in db.StoredFiles.AsNoTracking() on v.FileId equals f.Id
                where j.Id == id && j.RequestedBy == uid && v.CandidateId == uid && f.OwnerUserId == uid && f.Status == "ready"
                select new { Job = j, Method = m, v.DocumentHash }).SingleOrDefaultAsync(ct);
            if (row is null) return Results.NotFound();
            if (row.Job.Status is not ("succeeded" or "failed")) return Results.Problem(statusCode: 409, title: "capture_not_terminal");
            var parameters = JsonSerializer.Deserialize<MethodParameters>(row.Method.Parameters, OutputValidator.Json);
            if (parameters?.Contract != FullCvExtraction.Contract || parameters.CompletionPolicy != CompletionValidator.Version ||
                parameters.ParserVersion != DocumentParser.Version)
                return Results.Problem(statusCode: 409, title: "capture_metadata_unavailable");
            var attempts = await db.ExternalCallAttempts.AsNoTracking().Where(x => x.AiJobId == id).ToListAsync(ct);
            return Results.Ok(new { schemaVersion = 1, contract = parameters.Contract, parser = parameters.ParserVersion,
                row.Method.Provider, row.Method.Model, method = row.Method.ConfigHash,
                promptSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(FullCvExtraction.Prompt))),
                parameters = JsonSerializer.Deserialize<JsonElement>(row.Method.Parameters), beforeUserCorrection = true,
                unit = row.Method.Provider == "Fake" ? "test_credit" : "usd",
                document = new { sourceJobId = id, sourceSha256 = row.DocumentHash, rawOutput = row.Job.OutputJson,
                    finishReason = row.Job.Status == "succeeded" ? "stop" : null, errorCode = row.Job.ErrorCode,
                    inputTokens = attempts.Count > 0 && attempts.All(x => x.InputTokens.HasValue) ? (long?)attempts.Sum(x => x.InputTokens!.Value) : null,
                    outputTokens = attempts.Count > 0 && attempts.All(x => x.OutputTokens.HasValue) ? (long?)attempts.Sum(x => x.OutputTokens!.Value) : null,
                    cost = attempts.Count > 0 && attempts.All(x => x.ActualAmount.HasValue) ? (decimal?)attempts.Sum(x => x.ActualAmount!.Value) : null,
                    elapsedMs = row.Job.FinishedAt.HasValue ? (long?)(row.Job.FinishedAt.Value - row.Job.CreatedAt).TotalMilliseconds : null } });
        }).RequireAuthorization();
        app.MapGet("/api/ai/jobs/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDb db, PairMatchingService pairService, CancellationToken ct) =>
        {
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (await db.AiJobs.AnyAsync(x => x.Id == id && x.RequestedBy == uid && x.JobType == "match_pair", ct))
            {
                var match = await pairService.Read(uid, id, ct);
                return match is null ? Results.NotFound() : Results.Ok(match);
            }
            var result = await (from j in db.AiJobs.AsNoTracking() join m in db.AiMethods on j.MethodVersionId equals m.Id
                where j.Id == id && j.RequestedBy == uid
                select new { j.Id, j.Status, j.ErrorCode, j.OutputJson, j.ResumeVersionId, m.Provider, m.Model, m.PromptVersion, m.SchemaVersion }).SingleOrDefaultAsync(ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization();
    }
}
