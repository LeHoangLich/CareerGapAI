using System.Security.Claims;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Matching;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Writing;

public sealed record CreateJobRewriteRequest(Guid IdempotencyKey);

public sealed class JobRewriteService(AppDb db, JobService jobs)
{
    public static Task<bool> CanAccess(AppDb db, Guid uid, JobRewriteInput input, CancellationToken ct) =>
        (from j in db.Jobs.AsNoTracking()
         join c in db.Companies.AsNoTracking() on j.CompanyId equals c.Id
         join v in db.JobVersions.AsNoTracking() on input.JobVersionId equals v.Id
         join r in db.JobDataRevisions.AsNoTracking() on input.JobRevisionId equals r.Id
         where j.Id == input.JobId && c.OwnerUserId == uid && c.ArchivedAt == null && input.RecruiterId == uid &&
               j.Status != "archived" && v.JobId == j.Id && r.JobVersionId == v.Id &&
               db.Users.Any(u => u.Id == uid && u.AccountStatus == "active") &&
               (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId == uid && role.Name == "Recruiter" select ur).Any()
         select j.Id).AnyAsync(ct);

    public async Task<AiJob> Enqueue(Guid uid, Guid jobId, CreateJobRewriteRequest request, CancellationToken ct)
    {
        var previous = await db.AiJobs.AsNoTracking().SingleOrDefaultAsync(x => x.RequestedBy == uid && x.IdempotencyKey == request.IdempotencyKey, ct);
        if (previous is not null)
        {
            var pinned = await db.JobRewriteInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == previous.Id, ct);
            if (previous.JobType != "rewrite_jd" || pinned is null || pinned.JobId != jobId)
                throw new JobError("idempotency_conflict", 409);
            if (!await CanAccess(db, uid, pinned, ct)) throw new JobError("not_found", 404);
            return previous;
        }

        var job = await db.Jobs.Include(x => x.Company).Include(x => x.CurrentVersion).AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == jobId && x.Company!.OwnerUserId == uid && x.Company.ArchivedAt == null, ct)
            ?? throw new JobError("not_found", 404);
        if (!await BusinessIdentityEndpoints.Primary(db, uid, job.CompanyId, ct))
            throw new JobError("business_primary_required", 409);
        if (job.Status is "closed" or "archived" || job.CurrentVersion is null)
            throw new JobError("job_not_editable", 409);

        var revision = await db.JobDataRevisions.AsNoTracking()
            .Where(x => x.JobVersionId == job.CurrentVersion.Id)
            .OrderByDescending(x => x.RevisionNo)
            .FirstOrDefaultAsync(ct);
        if (revision is null)
        {
            await using var revTx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM job_versions WHERE id = {job.CurrentVersion.Id} FOR UPDATE", ct);
            revision = await db.JobDataRevisions.AsNoTracking()
                .Where(x => x.JobVersionId == job.CurrentVersion.Id)
                .OrderByDescending(x => x.RevisionNo)
                .FirstOrDefaultAsync(ct);
            if (revision is null)
            {
                if (!JobRevisionFactory.TryExtract(job.CurrentVersion.Id, null, 1, job.CurrentVersion.RequirementsText, DateTimeOffset.UtcNow, out var createdRev, out _) || createdRev is null)
                    throw new JobError("job_revision_unavailable", 409);
                db.JobDataRevisions.Add(createdRev);
                try
                {
                    await db.SaveChangesAsync(ct);
                    await revTx.CommitAsync(ct);
                    revision = createdRev;
                }
                catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
                {
                    await revTx.RollbackAsync(ct);
                    db.ChangeTracker.Clear();
                    revision = await db.JobDataRevisions.AsNoTracking()
                        .Where(x => x.JobVersionId == job.CurrentVersion.Id)
                        .OrderByDescending(x => x.RevisionNo)
                        .FirstAsync(ct);
                }
            }
            else
            {
                await revTx.CommitAsync(ct);
            }
        }

        var payload = PairMatchingContract.Serialize(JobRewriteContract.Build(job.CurrentVersion));
        var input = new JobRewriteInput
        {
            RecruiterId = uid,
            JobId = job.Id,
            JobVersionId = job.CurrentVersion.Id,
            JobRevisionId = revision.Id,
            Payload = payload,
            Fingerprint = PairMatchingContract.Hash($"{job.CurrentVersion.Id}:{job.CurrentVersion.ContentHash}:{revision.Id}:{payload}")
        };
        if (!await CanAccess(db, uid, input, ct)) throw new JobError("not_found", 404);
        return await jobs.EnqueueJobRewrite(uid, request.IdempotencyKey, input, ct);
    }

    public async Task<object?> Read(Guid uid, Guid aiJobId, CancellationToken ct)
    {
        var input = await db.JobRewriteInputs.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == aiJobId, ct);
        if (input is null || !await CanAccess(db, uid, input, ct)) return null;
        var job = await db.AiJobs.AsNoTracking().SingleAsync(x => x.Id == aiJobId && x.RequestedBy == uid, ct);
        var result = await db.JobRewriteResults.AsNoTracking().SingleOrDefaultAsync(x => x.AiJobId == aiJobId, ct);
        var provider = await db.AiMethods.Where(x => x.Id == job.MethodVersionId).Select(x => x.Provider).SingleAsync(ct);
        return new
        {
            job.Id,
            job.Status,
            job.ErrorCode,
            provider,
            input.JobId,
            input.JobVersionId,
            sections = result is null ? null : JsonSerializer.Deserialize<JobRewriteSections>(result.Sections, OutputValidator.Json)
        };
    }

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/jobs/{jobId:guid}/rewrite-proposals", async (Guid jobId, CreateJobRewriteRequest input, ClaimsPrincipal user, JobRewriteService service, CancellationToken ct) =>
        {
            try
            {
                var job = await service.Enqueue(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), jobId, input, ct);
                return Results.Accepted($"/api/writing/jd-proposals/{job.Id}", new { job.Id, job.Status });
            }
            catch (JobError e) { return Results.Problem(statusCode: e.Status, title: e.Code); }
        }).RequireAuthorization(p => p.RequireRole("Recruiter")).RequireRateLimiting("matching");

        app.MapGet("/api/jobs/{jobId:guid}/rewrite-proposals/latest", async (Guid jobId, ClaimsPrincipal user, AppDb db, JobRewriteService service, CancellationToken ct) =>
        {
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!await db.Jobs.AsNoTracking().AnyAsync(x => x.Id == jobId && x.Company!.OwnerUserId == uid, ct))
                return Results.NotFound();
            var id = await db.JobRewriteInputs.Where(x => x.RecruiterId == uid && x.JobId == jobId)
                .Join(db.AiJobs, x => x.AiJobId, x => x.Id, (i, j) => j)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(ct);
            return Results.Ok(new { proposal = id is null ? null : await service.Read(uid, id.Value, ct) });
        }).RequireAuthorization(p => p.RequireRole("Recruiter"));

        app.MapGet("/api/writing/jd-proposals/{id:guid}", async (Guid id, ClaimsPrincipal user, JobRewriteService service, CancellationToken ct) =>
        {
            var value = await service.Read(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), id, ct);
            return value is null ? Results.NotFound() : Results.Ok(value);
        }).RequireAuthorization(p => p.RequireRole("Recruiter"));
    }
}
