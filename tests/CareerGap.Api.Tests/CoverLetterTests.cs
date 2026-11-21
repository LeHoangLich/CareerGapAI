using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CareerGap.Api.Applications;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class CoverLetterTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task LetterIsPinnedNormalizedIdempotentAndPrivateToApplicationParticipants()
    {
        var (candidate, candidateId) = await fixture.User();
        var (owner, _) = await fixture.User("Recruiter");
        var (other, _) = await fixture.User("Recruiter");
        var (otherCandidate, _) = await fixture.User();
        var (admin, _) = await fixture.User("Admin");
        var cv = await FoundationFixture.Upload(candidate);
        var version = cv.GetProperty("versionId").GetGuid();
        var job = await Job(owner);
        var key = Guid.NewGuid();
        const string letter = "Kính gửi nhà tuyển dụng,\nTôi quan tâm React.\n<script>alert('plain text')</script>";
        var response = await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new ApplyRequest(version, key, "  " + letter.Replace("\n", "\r\n") + "  "));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var application = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = application.GetProperty("id").GetGuid();
        Assert.Equal(letter, application.GetProperty("coverLetter").GetString());
        var retry = await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new ApplyRequest(version, key, letter));
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(id, (await retry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        foreach (var changed in new string?[] { "Different letter", null, "  " })
            Assert.Equal(HttpStatusCode.Conflict, (await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new ApplyRequest(version, key, changed))).StatusCode);
        foreach (var participant in new[] { candidate, owner })
            Assert.Equal(letter, (await participant.GetFromJsonAsync<JsonElement>($"/api/applications/{id}")).GetProperty("coverLetter").GetString());
        var mine = await candidate.GetFromJsonAsync<JsonElement>($"/api/applications/mine?jobId={job}");
        var forJob = await owner.GetFromJsonAsync<JsonElement>($"/api/jobs/{job}/applications?status=submitted");
        Assert.Equal(letter, Assert.Single(mine.GetProperty("items").EnumerateArray()).GetProperty("coverLetter").GetString());
        Assert.Equal(letter, Assert.Single(forJob.GetProperty("items").EnumerateArray()).GetProperty("coverLetter").GetString());
        foreach (var outsider in new[] { other, otherCandidate, admin })
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/applications/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/jobs/{job}/applications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.App.CreateClient().GetAsync($"/api/applications/{id}")).StatusCode);
        Assert.DoesNotContain("coverLetter", await (await fixture.App.CreateClient().GetAsync($"/api/jobs/{job}")).Content.ReadAsStringAsync(), StringComparison.Ordinal);
        foreach (var participant in new[] { candidate, owner })
            Assert.DoesNotContain("React", await (await participant.GetAsync("/api/notifications")).Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var stored = await db.Applications.AsNoTracking().SingleAsync(x => x.Id == id);
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { candidateId, jobId = job, resumeVersionId = version, coverLetter = letter })));
        Assert.Equal(expectedHash, stored.SubmissionHash);
        var mutation = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE applications SET cover_letter = {'X'.ToString()} WHERE id = {id}"));
        Assert.Equal("23514", mutation.SqlState);
        var reviewing = await owner.PutAsJsonAsync($"/api/applications/{id}/status", new { rowVersion = 0, status = "reviewing" });
        Assert.Equal(HttpStatusCode.OK, reviewing.StatusCode);
        Assert.Equal(letter, (await reviewing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("coverLetter").GetString());
        Assert.Equal(1, await db.Applications.CountAsync(x => x.CandidateId == candidateId && x.JobId == job));
        Assert.Equal(1, await db.ResumeVersions.CountAsync(x => x.CandidateId == candidateId));
    }

    [Fact]
    public async Task OptionalLettersKeepLegacyHashesAndEachReapplicationHasItsOwnSnapshot()
    {
        var (candidate, candidateId) = await fixture.User();var (owner, _) = await fixture.User("Recruiter");
        var version = (await FoundationFixture.Upload(candidate)).GetProperty("versionId").GetGuid();
        var job = await Job(owner);var key = Guid.NewGuid();
        var response = await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new { resumeVersionId = version, submissionKey = key });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var application = await response.Content.ReadFromJsonAsync<JsonElement>();var id = application.GetProperty("id").GetGuid();
        Assert.Equal(JsonValueKind.Null, application.GetProperty("coverLetter").ValueKind);
        foreach (var empty in new string?[] { null, "", " \r\n\t " })
            Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new ApplyRequest(version, key, empty))).StatusCode);
        using var scope = fixture.App.Services.CreateScope();var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var oldHash = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { candidateId, jobId = job, resumeVersionId = version })));
        Assert.Equal(oldHash, (await db.Applications.AsNoTracking().SingleAsync(x => x.Id == id)).SubmissionHash);
        Assert.Equal(HttpStatusCode.Conflict, (await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new ApplyRequest(version, key, "Cannot add a letter to the old submission"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync($"/api/applications/{id}/withdraw", new { rowVersion = 0 })).StatusCode);
        const string secondLetter = "Thư cho lượt ứng tuyển mới.";
        var second = await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new ApplyRequest(version, Guid.NewGuid(), secondLetter));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(secondLetter, (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("coverLetter").GetString());
        Assert.Null((await db.Applications.AsNoTracking().SingleAsync(x => x.Id == id)).CoverLetter);
        Assert.Equal(oldHash, (await db.Applications.AsNoTracking().SingleAsync(x => x.Id == id)).SubmissionHash);
    }

    [Fact]
    public async Task InvalidLettersAreRejectedWithoutCreatingSubmissionsAndTheBoundaryIsAccepted()
    {
        var (candidate, _) = await fixture.User();var (owner, _) = await fixture.User("Recruiter");
        var version = (await FoundationFixture.Upload(candidate)).GetProperty("versionId").GetGuid();var job = await Job(owner);
        foreach (var invalid in new[] { new string('a', 5001), new string(' ', 5001), "bad\0text" })
        {
            var response = await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new ApplyRequest(version, Guid.NewGuid(), invalid));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("invalid_cover_letter", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        }
        using var scope = fixture.App.Services.CreateScope();var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.False(await db.Applications.AnyAsync(x => x.JobId == job));
        var accepted = await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new ApplyRequest(version, Guid.NewGuid(), new string('a', 5000)));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal(5000, (await accepted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("coverLetter").GetString()!.Length);
    }

    [Fact]
    public async Task LetterFollowsTheExistingNinetyDayRetentionWithoutDeletingTheOwnedCv()
    {
        var (candidate, candidateId) = await fixture.User();var (owner, _) = await fixture.User("Recruiter");
        var version = (await FoundationFixture.Upload(candidate)).GetProperty("versionId").GetGuid();var job = await Job(owner);
        var response = await candidate.PostAsJsonAsync($"/api/jobs/{job}/applications", new ApplyRequest(version, Guid.NewGuid(), "Private retention fixture"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync($"/api/applications/{id}/withdraw", new { rowVersion = 0 })).StatusCode);
        using var scope = fixture.App.Services.CreateScope();var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Applications.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.StatusChangedAt, fixture.Clock.GetUtcNow().AddDays(-91)));
        Assert.Equal(HttpStatusCode.NotFound, (await candidate.GetAsync($"/api/applications/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/applications/{id}")).StatusCode);
        Assert.True(await scope.ServiceProvider.GetRequiredService<ApplicationRetentionService>().PurgeExpiredAsync(default) >= 1);
        Assert.False(await db.Applications.AnyAsync(x => x.Id == id));
        Assert.True(await db.ResumeVersions.AnyAsync(x => x.Id == version && x.CandidateId == candidateId));
        Assert.False(await db.Notifications.AnyAsync(x => x.ApplicationId == id));
    }

    private async Task<Guid> Job(HttpClient owner)
    {
        var companyResponse = await owner.PostAsJsonAsync("/api/companies", new { name = "F11 synthetic " + Guid.NewGuid().ToString("N")[..8] });
        Assert.Equal(HttpStatusCode.Created, companyResponse.StatusCode);
        var companyId = (await companyResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var response = await owner.PostAsJsonAsync("/api/jobs", new { companyId, content = new { title = "F11 test role", description = "Test only", requirementsText = "Testing", employmentType = "full_time", workMode = "remote", language = "vi" } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<JsonElement>();var id = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await fixture.PublishWithVerifiedBusinessFixture(owner, id, new { rowVersion = job.GetProperty("rowVersion").GetInt64() })).StatusCode);
        return id;
    }
}
