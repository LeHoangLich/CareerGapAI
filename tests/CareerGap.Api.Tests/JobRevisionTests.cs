using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class JobRevisionTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task JobOwnerReviewsEvidenceAndConfirmsRequirementsForOneImmutableVersion()
    {
        var (owner, _) = await fixture.User("Recruiter");
        var (otherRecruiter, _) = await fixture.User("Recruiter");
        var (candidate, _) = await fixture.User("Candidate");
        var companyResponse = await owner.PostAsJsonAsync("/api/companies", new
        {
            name = $"Revision company {Guid.NewGuid():N}"[..26], description = "Fixture company"
        });
        Assert.Equal(HttpStatusCode.Created, companyResponse.StatusCode);
        var company = await companyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var requirementsText = "Bắt buộc: C#\nƯu tiên: PostgreSQL\nASP.NET Core";
        var jobResponse = await owner.PostAsJsonAsync("/api/jobs", new
        {
            companyId = company.GetProperty("id").GetGuid(), applicationDeadline = (DateTimeOffset?)null,
            content = new
            {
                title = "JD revision fixture", description = "Local-only test JD", requirementsText,
                benefitsText = (string?)null, industry = "Technology", location = "Remote",
                employmentType = "full_time", workMode = "remote", experienceMin = (decimal?)null,
                experienceMax = (decimal?)null, salaryMin = (decimal?)null, salaryMax = (decimal?)null,
                salaryCurrency = (string?)null, salaryPeriod = (string?)null, language = "vi"
            }
        });
        Assert.Equal(HttpStatusCode.Created, jobResponse.StatusCode);
        var job = await jobResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = job.GetProperty("id").GetGuid();
        var versionId = job.GetProperty("version").GetProperty("id").GetGuid();
        var path = $"/api/recruiter/jobs/{jobId}/versions/{versionId}/revisions";

        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.PostAsync(path + "/extract", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await candidate.GetAsync(path)).StatusCode);
        var extractResponse = await owner.PostAsync(path + "/extract", null);
        Assert.Equal(HttpStatusCode.Created, extractResponse.StatusCode);
        var extracted = await extractResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstRevisionId = extracted.GetProperty("id").GetGuid();
        Assert.Equal("local_extracted", extracted.GetProperty("origin").GetString());
        Assert.Equal("jd-lines-importance-hints-v1", extracted.GetProperty("parserVersion").GetString());
        var sourceRequirements = extracted.GetProperty("structuredData").GetProperty("requirements");
        Assert.Equal(3, sourceRequirements.GetArrayLength());
        Assert.Equal("required", sourceRequirements[0].GetProperty("importance").GetString());
        Assert.Equal("optional", sourceRequirements[1].GetProperty("importance").GetString());
        Assert.Equal("unspecified", sourceRequirements[2].GetProperty("importance").GetString());
        Assert.Equal(requirementsText, extracted.GetProperty("sourceText").GetProperty("requirementsText").GetString());
        var sourceEvidence = extracted.GetProperty("evidence")[1];
        Assert.Equal("Ưu tiên: PostgreSQL", sourceEvidence.GetProperty("quote").GetString());
        Assert.Equal("document_text", sourceEvidence.GetProperty("sourceType").GetString());
        var startOffset = sourceEvidence.GetProperty("startOffset").GetInt32();
        var endOffset = sourceEvidence.GetProperty("endOffset").GetInt32();
        Assert.Equal("Ưu tiên: PostgreSQL", requirementsText[startOffset..endOffset]);

        var repeatedExtract = await owner.PostAsync(path + "/extract", null);
        Assert.Equal(HttpStatusCode.OK, repeatedExtract.StatusCode);
        var repeated = await repeatedExtract.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(firstRevisionId, repeated.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await otherRecruiter.PostAsJsonAsync(path + $"/{firstRevisionId}/confirm", new
        {
            baseRevisionNo = 1, acceptedRequirements = Array.Empty<object>(), userStatements = Array.Empty<object>()
        })).StatusCode);

        var selected = new[]
        {
            new { requirementId = sourceRequirements[0].GetProperty("requirementId").GetString(), importance = "required" },
            new { requirementId = sourceRequirements[2].GetProperty("requirementId").GetString(), importance = "optional" }
        };
        var confirmResponse = await owner.PostAsJsonAsync(path + $"/{firstRevisionId}/confirm", new
        {
            baseRevisionNo = 1, acceptedRequirements = selected,
            userStatements = new[] { new { text = "Docker basics", importance = "unspecified" } }
        });
        Assert.Equal(HttpStatusCode.Created, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user_confirmed", confirmed.GetProperty("origin").GetString());
        Assert.Equal(firstRevisionId, confirmed.GetProperty("parentRevisionId").GetGuid());
        Assert.Equal(3, confirmed.GetProperty("structuredData").GetProperty("requirements").GetArrayLength());
        Assert.Equal("optional", confirmed.GetProperty("structuredData").GetProperty("requirements")[1].GetProperty("importance").GetString());
        var manualEvidence = confirmed.GetProperty("evidence").EnumerateArray()
            .Single(x => x.GetProperty("sourceType").GetString() == "user_statement");
        Assert.Equal(JsonValueKind.Null, manualEvidence.GetProperty("quote").ValueKind);
        Assert.Equal(JsonValueKind.Null, manualEvidence.GetProperty("startOffset").ValueKind);
        Assert.Equal("Docker basics", manualEvidence.GetProperty("statement").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path + $"/{firstRevisionId}/confirm", new
        {
            baseRevisionNo = 1, acceptedRequirements = selected, userStatements = Array.Empty<object>()
        })).StatusCode);

        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var immutable = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE job_data_revisions SET origin = 'local_extracted' WHERE id = {confirmed.GetProperty("id").GetGuid()}"));
        Assert.Equal("23514", immutable.SqlState);

        var history = await owner.GetFromJsonAsync<JsonElement[]>(path);
        Assert.Equal(2, history!.Length);
        Assert.Equal("user_confirmed", history[0].GetProperty("origin").GetString());
    }
}
