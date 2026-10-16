using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class ResumeBuilderRevisionTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task CandidateCanSaveImmutableVersionedBuilderContentWithUserStatementProvenance()
    {
        var (candidate, candidateId) = await fixture.User();
        var (other, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(candidate, FoundationFixture.Docx("Synthetic CV file generated from entered content."), "builder.docx");
        var versionId = upload.GetProperty("versionId").GetGuid();
        var input = BuilderInput(0);

        using var created = await candidate.PostAsJsonAsync($"/api/resume-versions/{versionId}/revisions/manual", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var revision = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user_entered", revision.GetProperty("origin").GetString());
        Assert.Equal(2, revision.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(1, revision.GetProperty("revisionNo").GetInt32());
        Assert.Equal(candidateId, revision.GetProperty("confirmedBy").GetGuid());
        Assert.Equal("simple-v1", revision.GetProperty("structuredData").GetProperty("templateVersion").GetString());
        Assert.Equal("Nguyễn Ứng Viên", revision.GetProperty("structuredData").GetProperty("displayName").GetString());
        var evidenceJson = revision.GetProperty("evidence").GetRawText();
        Assert.Contains("user_statement", evidenceJson);
        Assert.Contains("skills[0]", evidenceJson);
        Assert.DoesNotContain("candidate@example.invalid", evidenceJson);
        Assert.DoesNotContain("React", evidenceJson);

        // An identical retry remains idempotent even though the submitted base revision is now stale.
        using var retry = await candidate.PostAsJsonAsync($"/api/resume-versions/{versionId}/revisions/manual", input);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retried = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(revision.GetProperty("id").GetGuid(), retried.GetProperty("id").GetGuid());

        using var next = await candidate.PostAsJsonAsync($"/api/resume-versions/{versionId}/revisions/manual", BuilderInput(1, "Kotlin"));
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        var nextRevision = await next.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, nextRevision.GetProperty("revisionNo").GetInt32());
        Assert.Equal(revision.GetProperty("id").GetGuid(), nextRevision.GetProperty("parentRevisionId").GetGuid());

        using var stale = await candidate.PostAsJsonAsync($"/api/resume-versions/{versionId}/revisions/manual", BuilderInput(0, "Vue"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var invalid = await candidate.PostAsJsonAsync($"/api/resume-versions/{versionId}/revisions/manual", new
        {
            baseRevisionNo = 2, templateVersion = "simple-v1", displayName = "Test", skills = new[] { "" },
            experience = Array.Empty<object>(), education = Array.Empty<object>(), projects = Array.Empty<object>(), certifications = Array.Empty<string>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/resume-versions/{versionId}/revisions/manual", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.PostAsJsonAsync($"/api/resume-versions/{versionId}/revisions/manual", input)).StatusCode);

        var revisionId = revision.GetProperty("id").GetGuid();
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE resume_data_revisions SET structured_data = '{{}}' WHERE id = {revisionId}"));
        Assert.Equal("23514", immutable.SqlState);
    }

    private static object BuilderInput(int baseRevisionNo, string skill = "React") => new
    {
        baseRevisionNo,
        templateVersion = "simple-v1",
        displayName = "Nguyễn Ứng Viên",
        email = "candidate@example.invalid",
        phone = "0900000000",
        location = "Hà Nội",
        headline = "Kỹ sư phần mềm",
        summary = "Ứng viên tự nhập phần tóm tắt bằng tiếng Việt.",
        skills = new[] { skill, "TypeScript" },
        experience = new[] { new { role = "Kỹ sư", organization = "Công ty mẫu", period = "2024–2026", description = "Mô tả do ứng viên cung cấp." } },
        education = new[] { new { program = "Cử nhân CNTT", institution = "Trường mẫu", period = "2020–2024", details = "" } },
        projects = new[] { new { name = "Ứng dụng mẫu", context = "Cá nhân", description = "Ứng viên tự mô tả dự án." } },
        certifications = new[] { "Chứng chỉ giả lập" }
    };
}
