using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class ResumeRevisionTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task CandidateCanConfirmExtractedSkillsWithoutConvertingStatementsIntoDocumentEvidence()
    {
        var (candidate, candidateId) = await fixture.User();
        var (other, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate,
            FoundationFixture.Docx("Synthetic CV. React and TypeScript are explicit; PostgreSQL is also listed."));
        var versionId = upload.GetProperty("versionId").GetGuid();
        var job = await fixture.Enqueue(candidateId, versionId);
        await ProcessJob(job.Id);

        using var revisionsResponse = await candidate.GetAsync($"/api/resume-versions/{versionId}/revisions");
        Assert.Equal(HttpStatusCode.OK, revisionsResponse.StatusCode);
        var revisions = await revisionsResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        var extracted = Assert.Single(revisions!);
        Assert.Equal("ai_extracted", extracted.GetProperty("origin").GetString());
        Assert.Equal(1, extracted.GetProperty("revisionNo").GetInt32());
        Assert.Equal(versionId, extracted.GetProperty("resumeVersionId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/resume-versions/{versionId}/revisions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync($"/api/resume-versions/{versionId}/revisions")).StatusCode);

        var revisionId = extracted.GetProperty("id").GetGuid();
        var invalid = await candidate.PostAsJsonAsync(
            $"/api/resume-versions/{versionId}/revisions/{revisionId}/confirm",
            new { baseRevisionNo = 1, acceptedSkillNames = new[] { "React", "A skill not found in the CV" }, userStatementSkills = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var confirmation = await candidate.PostAsJsonAsync(
            $"/api/resume-versions/{versionId}/revisions/{revisionId}/confirm",
            new { baseRevisionNo = 1, acceptedSkillNames = new[] { "React", "TypeScript" }, userStatementSkills = new[] { "Go" } });
        Assert.Equal(HttpStatusCode.Created, confirmation.StatusCode);
        var confirmed = await confirmation.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user_confirmed", confirmed.GetProperty("origin").GetString());
        Assert.Equal(2, confirmed.GetProperty("revisionNo").GetInt32());
        Assert.Equal(revisionId, confirmed.GetProperty("parentRevisionId").GetGuid());
        Assert.Equal(candidateId, confirmed.GetProperty("confirmedBy").GetGuid());
        Assert.Equal(JsonValueKind.String, confirmed.GetProperty("confirmedAt").ValueKind);

        var evidence = confirmed.GetProperty("evidence").EnumerateArray().ToArray();
        var documentEvidence = Assert.Single(evidence, x => x.GetProperty("sourceType").GetString() == "document_span" &&
            x.GetProperty("skillName").GetString() == "React");
        Assert.True(documentEvidence.GetProperty("startOffset").GetInt32() < documentEvidence.GetProperty("endOffset").GetInt32());
        Assert.Equal("React", documentEvidence.GetProperty("quote").GetString());
        var userEvidence = Assert.Single(evidence, x => x.GetProperty("sourceType").GetString() == "user_statement");
        Assert.Equal("Go", userEvidence.GetProperty("statement").GetString());
        Assert.Equal(JsonValueKind.Null, userEvidence.GetProperty("quote").ValueKind);
        Assert.Equal(JsonValueKind.Null, userEvidence.GetProperty("segmentId").ValueKind);

        using var stale = await candidate.PostAsJsonAsync(
            $"/api/resume-versions/{versionId}/revisions/{revisionId}/confirm",
            new { baseRevisionNo = 1, acceptedSkillNames = new[] { "React" }, userStatementSkills = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var confirmedId = confirmed.GetProperty("id").GetGuid();
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var update = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE resume_data_revisions SET data_hash = repeat('b',64) WHERE id = {confirmedId}"));
            Assert.Equal("23514", update.SqlState);
            var delete = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM resume_data_revisions WHERE id = {confirmedId}"));
            Assert.Equal("23514", delete.SqlState);
        }
    }

    private async Task ProcessJob(Guid jobId)
    {
        using var scope = fixture.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<JobService>();
        AiJob? claimed;
        do
        {
            claimed = await service.Claim("revision-api-test-worker", default);
            Assert.NotNull(claimed);
            await JobWorker.Process(scope.ServiceProvider, claimed.Id, "revision-api-test-worker", claimed.LeaseGeneration, default);
        } while (claimed.Id != jobId);
    }
}
