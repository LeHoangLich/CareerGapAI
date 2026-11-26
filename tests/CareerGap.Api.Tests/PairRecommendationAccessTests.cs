using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Matching;
using CareerGap.Api.Recruitment;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class PairRecommendationAccessTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task PublicRecommendationRefreshesProfileLabelAfterAwaitingPairAdmission()
    {
        const string password = "SyntheticBatch!Password-2026";
        var (owner, ownerId) = await fixture.User("Recruiter", password);
        var (candidate, candidateId) = await fixture.User();
        var upload = await FoundationFixture.Upload(candidate, FoundationFixture.Docx("React"));
        var versionId = upload.GetProperty("versionId").GetGuid();
        var companyResponse = await owner.PostAsJsonAsync("/api/companies", new
        {
            name = $"Synthetic batch {Guid.NewGuid():N}"[..30], description = "Synthetic access regression"
        });
        Assert.Equal(HttpStatusCode.Created, companyResponse.StatusCode);
        var companyId = (await companyResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        const string requirements = "Required: React";
        var jobResponse = await owner.PostAsJsonAsync("/api/jobs", new
        {
            companyId, applicationDeadline = DateTimeOffset.UtcNow.AddDays(10),
            content = new
            {
                title = "React Developer", description = "Synthetic React role", requirementsText = requirements,
                employmentType = "full_time", workMode = "remote", language = "en"
            }
        });
        Assert.Equal(HttpStatusCode.Created, jobResponse.StatusCode);
        var job = await jobResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await fixture.PublishWithVerifiedBusinessFixture(owner, jobId,
            new { rowVersion = job.GetProperty("rowVersion").GetInt64() })).StatusCode);

        string ownerEmail;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var profile = await db.CandidateProfiles.SingleAsync(x => x.UserId == candidateId);
            profile.DisplayName = "Synthetic shared batch name";
            db.CandidateProfileVisibility.Add(new CandidateProfileVisibility { CandidateId = candidateId, FieldKey = "display_name" });
            // A document-backed synthetic fixture ensures public-pool admission is eligible.
            var confirmedCv = new ResumeDataRevision
            {
                CandidateId = candidateId, ResumeVersionId = versionId, RevisionNo = 1,
                Origin = "user_confirmed", SchemaVersion = 1,
                StructuredData = JsonSerializer.Serialize(new ResumeStructuredSkills(1, [new("React")]), OutputValidator.Json),
                SourceText = JsonSerializer.Serialize(new[] { new SourceSegment("synthetic", "React") }, OutputValidator.Json),
                Evidence = JsonSerializer.Serialize(new[] { new ResumeSkillEvidence("React", "document_span", "synthetic", 0, 5, "React", null) }, OutputValidator.Json),
                DataHash = new string('a', 64), ParserVersion = "synthetic-v1",
                ConfirmedBy = candidateId, ConfirmedAt = fixture.Clock.GetUtcNow()
            };
            db.ResumeDataRevisions.Add(confirmedCv);
            var jobVersionId = (await db.Jobs.AsNoTracking().SingleAsync(x => x.Id == jobId)).CurrentVersionId!.Value;
            Assert.True(JobRevisionFactory.TryExtract(jobVersionId, null, 1, requirements,
                fixture.Clock.GetUtcNow(), out var extracted, out _));
            var parsed = JsonSerializer.Deserialize<JobStructuredRequirements>(extracted!.StructuredData,
                CareerGap.Api.Ai.OutputValidator.Json)!;
            Assert.True(JobRevisionFactory.TryConfirm(extracted, ownerId, 2,
                new(1, parsed.Requirements.Select(x => new JobRequirementSelection(x.RequirementId, x.Importance)).ToArray(), []),
                fixture.Clock.GetUtcNow(), out var confirmed, out _));
            db.JobDataRevisions.AddRange(extracted, confirmed!);
            await db.SaveChangesAsync();
            var storedJobVersion = await db.JobVersions.AsNoTracking().SingleAsync(x => x.Id == jobVersionId);
            Assert.NotEmpty(PairMatchingContract.Build(confirmedCv, confirmed!, storedJobVersion, publicScope: true).Facts);
            ownerEmail = (await db.Users.SingleAsync(x => x.Id == ownerId)).Email!;
        }
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync("/api/resumes/public",
            new { resumeVersionId = versionId, expectedRowVersion = (long?)null })).StatusCode);

        var barrier = new AdmissionBarrier();
        await using var gatedApp = fixture.App.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDb>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<AppDb>>();
            services.AddDbContext<AppDb>(options => options
                .UseNpgsql(fixture.Connection, pg => pg.SetPostgresVersion(16, 0)).AddInterceptors(barrier));
        }));
        using var gatedOwner = gatedApp.CreateClient();
        await FoundationFixture.Csrf(gatedOwner);
        Assert.Equal(HttpStatusCode.NoContent, (await gatedOwner.PostAsJsonAsync("/api/auth/login",
            new { email = ownerEmail, password })).StatusCode);
        await FoundationFixture.Csrf(gatedOwner);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var pending = gatedOwner.PostAsJsonAsync("/api/matching/recommendations",
            new PairBatchRequest("public_resumes", Guid.NewGuid(), JobId: jobId, TopK: 1), timeout.Token);
        try
        {
            // Admission begins only after baseline has returned its label. Revocation here
            // exercises the second await boundary, after baseline's own final recheck.
            var first = await Task.WhenAny(barrier.AdmissionStarted, pending).WaitAsync(TimeSpan.FromSeconds(20));
            if (first == pending)
            {
                using var early = await pending;
                Assert.Fail($"Batch completed before reaching admission barrier: {(int)early.StatusCode} {await early.Content.ReadAsStringAsync()}");
            }
            await barrier.AdmissionStarted;
            var profile = await candidate.GetFromJsonAsync<JsonElement>("/api/profile/me");
            Assert.Equal(HttpStatusCode.OK, (await candidate.PutAsJsonAsync("/api/profile/me", new
            {
                rowVersion = profile.GetProperty("rowVersion").GetInt64(),
                displayName = "Synthetic shared batch name", details = new { }, sharedFields = Array.Empty<string>()
            })).StatusCode);
        }
        finally { barrier.Release(); }

        using var response = await pending;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PairBatchView>();
        var item = Assert.Single(result!.Items);
        Assert.Equal(versionId, item.ResumeVersionId);
        Assert.NotNull(item.PairJobId);
        Assert.Equal(1, result.SelectedCount);
        Assert.Equal("Ứng viên", item.Label);
    }

    private sealed class AdmissionBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed = 1;
        public Task AdmissionStarted => started.Task;
        public void Release() => released.TrySetResult();
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock(341017)", StringComparison.Ordinal) &&
                Interlocked.Exchange(ref armed, 0) == 1)
            {
                started.TrySetResult();
                await released.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
