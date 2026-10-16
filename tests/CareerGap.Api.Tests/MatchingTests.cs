using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class MatchingTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task CandidateMatchesPrivateResumeOnlyAgainstCurrentOpenPublishedJobs()
    {
        var (owner, _) = await fixture.User("Recruiter");
        var (candidate, _) = await fixture.User();
        var (anotherCandidate, _) = await fixture.User();
        var (otherRecruiter, _) = await fixture.User("Recruiter");
        var companyId = await CreateCompany(owner);
        var job = await CreateJob(owner, companyId, "Backend Engineer", "C# PostgreSQL REST API", publish: true);
        var noTerms = await CreateJob(owner, companyId, "the and", "the and or", publish: true, description: "the and or");
        var draft = await CreateJob(owner, companyId, "Draft Backend", "C# PostgreSQL", publish: false);
        var closed = await CreateJob(owner, companyId, "Closed Backend", "C# PostgreSQL", publish: true, close: true);
        var expired = await CreateJob(owner, companyId, "Expired Backend", "C# PostgreSQL", publish: true);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE jobs SET application_deadline = {fixture.Clock.GetUtcNow().AddDays(-1)} WHERE id = {expired}");
        }
        var resume = await FoundationFixture.Upload(candidate,
            FoundationFixture.Docx("Synthetic backend engineer. Skills C# PostgreSQL REST API."));
        var versionId = resume.GetProperty("versionId").GetGuid();

        using var response = await candidate.GetAsync($"/api/matching/jobs?resumeVersionId={versionId}&topK=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("retrieval_only", run.GetProperty("scoreKind").GetString());
        Assert.Equal("weighted-keyword-overlap-v1", run.GetProperty("methodVersion").GetString());
        Assert.True(run.GetProperty("noQueryTermsCount").GetInt32() >= 1);
        var items = run.GetProperty("items").EnumerateArray().ToArray();
        var match = Assert.Single(items, x => x.GetProperty("jobId").GetGuid() == job);
        Assert.DoesNotContain(items, x => x.GetProperty("jobId").GetGuid() == draft);
        Assert.DoesNotContain(items, x => x.GetProperty("jobId").GetGuid() == closed);
        Assert.DoesNotContain(items, x => x.GetProperty("jobId").GetGuid() == expired);
        Assert.DoesNotContain(items, x => x.GetProperty("jobId").GetGuid() == noTerms);
        Assert.True(run.GetProperty("noQueryTermsCount").GetInt32() >= 1);
        Assert.True(run.GetProperty("searchSpaceCount").GetInt32() >= 1);
        var score = match.GetProperty("retrievalScore").GetDouble();
        Assert.InRange(score, 0d, 1d);
        Assert.True(score > 0);
        Assert.Contains(match.GetProperty("keywordOverlap").GetProperty("terms").EnumerateArray(),
            x => x.GetString() == "postgresql");
        Assert.Equal(HttpStatusCode.NotFound,
            (await anotherCandidate.GetAsync($"/api/matching/jobs?resumeVersionId={versionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.GetAsync($"/api/matching/jobs?resumeVersionId={versionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await candidate.GetAsync($"/api/matching/jobs?resumeVersionId={versionId}&topK=51")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherRecruiter.GetAsync($"/api/matching/public-resumes?jobId={job}")).StatusCode);
    }

    [Fact]
    public async Task CandidateMatchingAddsConfirmedRevisionDiagnosticWithoutReplacingBaselineRetrieval()
    {
        var (owner, ownerId) = await fixture.User("Recruiter");
        var (candidate, candidateId) = await fixture.User();
        var companyId = await CreateCompany(owner);
        var jobId = await CreateJob(owner, companyId, "Backend Engineer", "C#\nGo", publish: true);
        var uploaded = await FoundationFixture.Upload(candidate,
            FoundationFixture.Docx("Synthetic backend engineer. Skills C# PostgreSQL REST API."));
        var resumeVersionId = uploaded.GetProperty("versionId").GetGuid();
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var jobVersionId = await db.Jobs.Where(x => x.Id == jobId).Select(x => x.CurrentVersionId!.Value).SingleAsync();
            db.ResumeDataRevisions.Add(new ResumeDataRevision
            {
                CandidateId = candidateId,
                ResumeVersionId = resumeVersionId,
                RevisionNo = 1,
                Origin = "user_confirmed",
                SchemaVersion = 1,
                StructuredData = JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    skills = new[] { new { name = "C#" }, new { name = "Go" } }
                }),
                SourceText = JsonSerializer.Serialize(new[] { new SourceSegment("synthetic", "C# PostgreSQL") }),
                Evidence = JsonSerializer.Serialize(new object[]
                {
                    new { skillName = "C#", sourceType = "document_span", segmentId = "synthetic", startOffset = 0, endOffset = 2, quote = "C#", statement = (string?)null },
                    new { skillName = "Go", sourceType = "user_statement", segmentId = (string?)null, startOffset = (int?)null, endOffset = (int?)null, quote = (string?)null, statement = "Go" }
                }),
                DataHash = new string('a', 64),
                ParserVersion = "synthetic-v1",
                ConfirmedBy = candidateId,
                ConfirmedAt = fixture.Clock.GetUtcNow()
            });
            const string requirementsText = "C#\nGo";
            db.JobDataRevisions.Add(new JobDataRevision
            {
                JobVersionId = jobVersionId,
                RevisionNo = 1,
                Origin = "user_confirmed",
                SchemaVersion = 1,
                StructuredData = JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    requirements = new[]
                    {
                        new { requirementId = "source-0001", text = "C#", importance = "required", sourceType = "document_text" },
                        new { requirementId = "source-0002", text = "Go", importance = "required", sourceType = "document_text" }
                    }
                }),
                SourceText = JsonSerializer.Serialize(new { requirementsText }),
                Evidence = JsonSerializer.Serialize(new object[]
                {
                    new { requirementId = "source-0001", sourceType = "document_text", startOffset = 0, endOffset = 2, quote = "C#", statement = (string?)null },
                    new { requirementId = "source-0002", sourceType = "document_text", startOffset = 3, endOffset = 5, quote = "Go", statement = (string?)null }
                }),
                DataHash = new string('b', 64),
                ParserVersion = "synthetic-v1",
                ConfirmedBy = ownerId,
                ConfirmedAt = fixture.Clock.GetUtcNow()
            });
            await db.SaveChangesAsync();
        }

        using var response = await candidate.GetAsync($"/api/matching/jobs?resumeVersionId={resumeVersionId}&topK=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("weighted-keyword-overlap-v1", run.GetProperty("methodVersion").GetString());
        var match = Assert.Single(run.GetProperty("items").EnumerateArray(), x => x.GetProperty("jobId").GetGuid() == jobId);
        var structured = match.GetProperty("confirmedRevisionMatch");
        Assert.Equal("confirmed-revision-term-overlap-diagnostic-v1", structured.GetProperty("methodVersion").GetString());
        Assert.Equal("ready", structured.GetProperty("status").GetString());
        Assert.Equal(2, structured.GetProperty("matchedRequiredRequirementCount").GetInt32());
        Assert.Equal(2, structured.GetProperty("matchedRequirementCount").GetInt32());
        Assert.False(match.TryGetProperty("compatibilityScore", out _));

        using var publish = await candidate.PostAsJsonAsync("/api/resumes/public",
            new { resumeVersionId, expectedRowVersion = (long?)null });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        using var publicSearch = await owner.GetAsync($"/api/matching/public-resumes?jobId={jobId}&topK=10");
        Assert.Equal(HttpStatusCode.OK, publicSearch.StatusCode);
        var publicRun = await publicSearch.Content.ReadFromJsonAsync<JsonElement>();
        var publicMatch = Assert.Single(publicRun.GetProperty("items").EnumerateArray());
        var publicDiagnostic = publicMatch.GetProperty("confirmedRevisionMatch");
        Assert.Equal("ready", publicDiagnostic.GetProperty("status").GetString());
        Assert.Equal(1, publicDiagnostic.GetProperty("eligibleSkillCount").GetInt32());
        Assert.Equal(1, publicDiagnostic.GetProperty("matchedRequiredRequirementCount").GetInt32());
        Assert.False(publicDiagnostic.TryGetProperty("skills", out _));
    }

    [Fact]
    public async Task RecruiterSearchesOnlyCurrentPublicResumeSelectionsForOwnedOpenJob()
    {
        var (owner, _) = await fixture.User("Recruiter");
        var (otherRecruiter, _) = await fixture.User("Recruiter");
        var (publicCandidate, publicCandidateId) = await fixture.User();
        var (privateCandidate, _) = await fixture.User();
        var companyId = await CreateCompany(owner);
        var jobId = await CreateJob(owner, companyId, "Backend Engineer", "C# .NET PostgreSQL REST API", publish: true);

        var publicResume = await FoundationFixture.Upload(publicCandidate,
            FoundationFixture.Docx("Experienced backend engineer with several synthetic years using C# .NET PostgreSQL, ASP.NET and REST APIs."));
        var publicVersionId = publicResume.GetProperty("versionId").GetGuid();
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var profile = await db.CandidateProfiles.SingleAsync(x => x.UserId == publicCandidateId);
            profile.DisplayName = "Private synthetic name";
            profile.Headline = "Private synthetic headline";
            await db.SaveChangesAsync();
        }
        using var publish = await publicCandidate.PostAsJsonAsync("/api/resumes/public",
            new { resumeVersionId = publicVersionId, expectedRowVersion = (long?)null });
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var privateResume = await FoundationFixture.Upload(privateCandidate,
            FoundationFixture.Docx("Private synthetic backend experience with C# .NET PostgreSQL."));

        using var response = await owner.GetAsync($"/api/matching/public-resumes?jobId={jobId}&topK=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var run = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("retrieval_only", run.GetProperty("scoreKind").GetString());
        Assert.Equal(0, run.GetProperty("noQueryTermsCount").GetInt32());
        Assert.Equal(1, run.GetProperty("searchSpaceCount").GetInt32());
        Assert.Equal(1, run.GetProperty("assessedCount").GetInt32());
        var item = Assert.Single(run.GetProperty("items").EnumerateArray());
        Assert.Equal(publicCandidateId, item.GetProperty("candidateId").GetGuid());
        Assert.Equal("Ứng viên", item.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("headline").ValueKind);
        Assert.NotEqual(privateResume.GetProperty("versionId").GetGuid(), item.GetProperty("resumeVersionId").GetGuid());
        Assert.InRange(item.GetProperty("retrievalScore").GetDouble(), 0d, 1d);
        Assert.Equal(HttpStatusCode.NotFound,
            (await otherRecruiter.GetAsync($"/api/matching/public-resumes?jobId={jobId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await publicCandidate.GetAsync($"/api/matching/public-resumes?jobId={jobId}")).StatusCode);

        var noTermsJobId = await CreateJob(owner, companyId, "the and", "the and or", publish: true, description: "the and or");
        run = await owner.GetFromJsonAsync<JsonElement>($"/api/matching/public-resumes?jobId={noTermsJobId}");
        Assert.Equal(run.GetProperty("searchSpaceCount").GetInt32(), run.GetProperty("noQueryTermsCount").GetInt32());
        Assert.Equal(0, run.GetProperty("unreadableCount").GetInt32());
        Assert.Empty(run.GetProperty("items").EnumerateArray());

        // Hiding the current selection removes it from the search immediately, even while the source CV remains private and readable by its owner.
        var selection = await publicCandidate.GetFromJsonAsync<JsonElement>("/api/resumes/public/me");
        Assert.Equal(HttpStatusCode.NoContent,
            (await publicCandidate.DeleteAsync($"/api/resumes/public/me?rowVersion={selection.GetProperty("rowVersion").GetInt64()}")).StatusCode);
        run = await owner.GetFromJsonAsync<JsonElement>($"/api/matching/public-resumes?jobId={jobId}");
        Assert.Equal(0, run.GetProperty("searchSpaceCount").GetInt32());
        Assert.Empty(run.GetProperty("items").EnumerateArray());
    }

    private static async Task<Guid> CreateCompany(HttpClient recruiter)
    {
        using var response = await recruiter.PostAsJsonAsync("/api/companies", new
        {
            name = $"Matching synthetic {Guid.NewGuid():N}"[..30], description = "Synthetic matching test company",
            location = "Hà Nội", industry = "Công nghệ", sizeLabel = "1-10"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateJob(HttpClient recruiter, Guid companyId, string title,
        string requirements, bool publish, bool close = false, string description = "Synthetic backend role for keyword matching tests.")
    {
        var content = new
        {
            title, description, requirementsText = requirements,
            benefitsText = "Synthetic benefits", industry = "Công nghệ", location = "Hà Nội",
            employmentType = "full_time", workMode = "remote", experienceMin = 1m, experienceMax = 5m,
            salaryMin = (decimal?)null, salaryMax = (decimal?)null, salaryCurrency = (string?)null,
            salaryPeriod = (string?)null, language = "en"
        };
        using var response = await recruiter.PostAsJsonAsync("/api/jobs", new
        {
            companyId, applicationDeadline = DateTimeOffset.UtcNow.AddDays(30), content
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = draft.GetProperty("id").GetGuid();
        if (publish)
        {
            using var publishResponse = await recruiter.PostAsJsonAsync($"/api/jobs/{id}/publish",
                new { rowVersion = draft.GetProperty("rowVersion").GetInt64() });
            Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
            if (close)
            {
                var published = await publishResponse.Content.ReadFromJsonAsync<JsonElement>();
                using var closeResponse = await recruiter.PostAsJsonAsync($"/api/jobs/{id}/close",
                    new { rowVersion = published.GetProperty("rowVersion").GetInt64() });
                Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
            }
        }
        return id;
    }
}
