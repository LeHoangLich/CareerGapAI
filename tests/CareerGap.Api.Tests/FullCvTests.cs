using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Matching;
using CareerGap.Api.Recruitment;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class FullCvTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    public const string Synthetic = "Name: Synthetic Candidate\nEmail: synthetic@example.invalid\nSummary: Builds synthetic software.\nSkills: React, TypeScript, PostgreSQL\nexperience[0].role: Developer\nexperience[0].organization: Example\nexperience[0].period: 2024–2026\nexperience[0].description: Built test software.\nprojects[0].name: Test project\nprojects[0].description: Built a synthetic fixture.\neducation[0].program: Computer Science\neducation[0].institution: Example University\ncertifications[0]: Synthetic certificate";

    [Fact]
    public async Task FullExtractionKeepsRawOutputAndOwnerConfirmationEditsHaveStatementProvenance()
    {
        var (owner, candidate) = await fixture.User();
        var (other, _) = await fixture.User();
        var (recruiter, _) = await fixture.User("Recruiter");
        var upload = await FoundationFixture.Upload(owner, FoundationFixture.Docx(Synthetic));
        var version = upload.GetProperty("versionId").GetGuid();
        var emptyStatus = await owner.GetFromJsonAsync<JsonElement>($"/api/resume-versions/{version}/extraction-status");
        Assert.Equal(JsonValueKind.Null, emptyStatus.GetProperty("job").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/resume-versions/{version}/extraction-status")).StatusCode);
        using var scope = fixture.App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<JobService>();
        var key = Guid.NewGuid();
        var queued = await service.Enqueue(candidate, new(version, key, FullCvExtraction.Contract), default);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.GetAsync($"/api/ai/jobs/{queued.Id}/evaluation-capture")).StatusCode);
        var same = await service.Enqueue(candidate, new(version, key, FullCvExtraction.Contract), default);
        Assert.Equal(queued.Id, same.Id);
        await Assert.ThrowsAsync<JobError>(() => service.Enqueue(candidate, new(version, key), default));
        var claimed = await service.Claim("full-cv-test", default); Assert.NotNull(claimed);
        Assert.Equal(queued.Id, claimed.Id);
        await JobWorker.Process(scope.ServiceProvider, queued.Id, "full-cv-test", claimed.LeaseGeneration, default);
        var db = scope.ServiceProvider.GetRequiredService<AppDb>(); db.ChangeTracker.Clear();
        var savedJob = await db.AiJobs.SingleAsync(x => x.Id == queued.Id);
        Assert.Equal("succeeded", savedJob.Status);
        var original = await db.ResumeDataRevisions.SingleAsync(x => x.AiJobId == queued.Id);
        Assert.Equal(3, original.SchemaVersion);
        Assert.True(FullCvExtraction.TryRead(original, out var fields, out _));
        Assert.Contains(fields, x => x.Path == "education[0].program" && x.Value == "Computer Science");
        var raw = FullCvExtraction.Validate(savedJob.OutputJson!, JsonSerializer.Deserialize<ParsedDocument>(JsonSerializer.Serialize(new DocumentParser().Parse(FoundationFixture.Docx(Synthetic), "fixture.docx", default)))!);
        Assert.Equal(fields.Length, raw.Fields.Count);
        var path = $"/api/resume-versions/{version}/revisions/{original.Id}/confirm-full";
        var changed = fields.Select(x => x.Path == "summary" ? x with { Value = "Candidate supplied correction" } : x).Append(new CvField("skills[3]", "Go")).ToArray();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(path, new ConfirmFullCvRequest(1, changed))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.PostAsJsonAsync(path, new ConfirmFullCvRequest(1, changed))).StatusCode);
        var response = await owner.PostAsJsonAsync(path, new ConfirmFullCvRequest(1, changed));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync(path, new ConfirmFullCvRequest(1, changed))).StatusCode);
        db.ChangeTracker.Clear();
        var confirmed = await db.ResumeDataRevisions.SingleAsync(x => x.ParentRevisionId == original.Id);
        Assert.True(FullCvExtraction.TryRead(confirmed, out _, out var evidence));
        Assert.Equal("user_statement", evidence.Single(x => x.Path == "summary").SourceType);
        Assert.Null(evidence.Single(x => x.Path == "summary").Quote);
        Assert.Equal("document_span", evidence.Single(x => x.Path == "displayName").SourceType);
        Assert.True(ResumeSkillProjection.TryRead(confirmed, false, out var privateSkills)); Assert.Contains("Go", privateSkills);
        Assert.True(ResumeSkillProjection.TryRead(confirmed, true, out var publicSkills)); Assert.DoesNotContain("Go", publicSkills);
        Assert.Equal(original.StructuredData, (await db.ResumeDataRevisions.AsNoTracking().SingleAsync(x => x.Id == original.Id)).StructuredData);
        var capture = await owner.GetFromJsonAsync<JsonElement>($"/api/ai/jobs/{queued.Id}/evaluation-capture");
        Assert.True(capture.GetProperty("beforeUserCorrection").GetBoolean());
        Assert.Equal(savedJob.OutputJson, capture.GetProperty("document").GetProperty("rawOutput").GetString());
        Assert.DoesNotContain("Candidate supplied correction", capture.GetProperty("document").GetProperty("rawOutput").GetString());
        Assert.Equal("stop", capture.GetProperty("document").GetProperty("finishReason").GetString());
        Assert.Equal("test_credit", capture.GetProperty("unit").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/ai/jobs/{queued.Id}/evaluation-capture")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await recruiter.GetAsync($"/api/ai/jobs/{queued.Id}/evaluation-capture")).StatusCode);
        var builder = await owner.GetFromJsonAsync<JsonElement>($"/api/resume-versions/{version}/builder-content");
        Assert.Equal("Candidate supplied correction", builder.GetProperty("summary").GetString());
        Assert.Equal("", builder.GetProperty("phone").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/resume-versions/{version}/builder-content")).StatusCode);
    }

    [Theory]
    [InlineData("{bad")]
    [InlineData("{\"fields\":[{\"path\":\"summary\",\"value\":\"invented\",\"quote\":\"React\",\"evidenceRef\":\"p1\"}]}")]
    [InlineData("{\"fields\":[{\"path\":\"skills[20]\",\"value\":\"React\",\"quote\":\"React\",\"evidenceRef\":\"p1\"}]}")]
    [InlineData("{\"fields\":[],\"score\":100}")]
    public void FullOutputRejectsMalformedUnsupportedFieldsAndInventedEvidence(string raw)
        => Assert.Throws<ProviderError>(() => FullCvExtraction.Validate(raw, new("application/pdf", [new("p1", "React")] )));

    [Fact]
    public void BuilderProjectionUsesSelfStatementsOnlyAndDoesNotMergeUnrelatedSkills()
    {
        var user = Guid.NewGuid();
        var input = new ResumeBuilderContentRequest(0, "simple-v1", "Synthetic", null, null, null, null, null,
            ["ReactJS", "PostgreSQL", "JavaScript"], [], [], [], []);
        Assert.True(ResumeBuilderRevisionFactory.TryPrepare(input, out var prepared, out _));
        var revision = ResumeBuilderRevisionFactory.Create(user, Guid.NewGuid(), null, 1, prepared!, DateTimeOffset.UtcNow);
        Assert.True(ResumeSkillProjection.TryRead(revision, false, out var own)); Assert.Equal(3, own.Count);
        Assert.True(ResumeSkillProjection.TryRead(revision, true, out var publicSkills)); Assert.Empty(publicSkills);
        var normalized = ResumeSkillProjection.CanonicalTerms(["reactjs", "postgresql", "javascript"]);
        Assert.Contains("react", normalized); Assert.DoesNotContain("sql", normalized); Assert.DoesNotContain("java", normalized);
    }

    [Fact]
    public void ParserReadsBodyHeaderFooterAndKillsDeadlineProcess()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart(); main.Document = new Document(new Body(new Paragraph(new Run(new Text("Synthetic body: React and testing.")))));
            main.AddNewPart<HeaderPart>().Header = new Header(new Paragraph(new Run(new Text("Họ tên: Ứng viên tổng hợp"))));
            main.AddNewPart<FooterPart>().Footer = new Footer(new Paragraph(new Run(new Text("Email: fixture@example.invalid"))));
        }
        var bytes = stream.ToArray();
        var result = new DocumentParser().Parse(bytes, "synthetic.docx", default);
        Assert.Contains(result.Segments, x => x.Region == "header" && x.Text.Contains("Ứng viên"));
        Assert.Contains(result.Segments, x => x.Region == "footer" && x.Paragraph == 1);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var error = Assert.Throws<DocumentError>(() => ParserProcess.Parse(bytes, "synthetic.docx", default, TimeSpan.FromMilliseconds(1)));
        Assert.Equal("parse_timeout", error.Code); Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => new DocumentParser().Parse(bytes, "synthetic.docx", cancelled.Token));
    }

    [Fact]
    public void JobAtomsPreserveAlternativesYearsAndExactSourceOffsets()
    {
        const string text = "Required: React; TypeScript\nTối thiểu 3-5 năm Java hoặc C#\nPreferred: Docker, Linux";
        Assert.True(JobRevisionFactory.TryExtract(Guid.NewGuid(), null, 1, text, DateTimeOffset.UtcNow, out var revision, out _));
        var data = JsonSerializer.Deserialize<JobStructuredRequirements>(revision!.StructuredData, OutputValidator.Json)!;
        Assert.Equal(5, data.Requirements.Count);
        var any = Assert.Single(data.Requirements, x => x.Relation == "any_of");
        Assert.Equal(3m, any.MinimumYears); Assert.Equal(2, any.Alternatives!.Count); Assert.True(any.NeedsReview);
        var evidence = JsonSerializer.Deserialize<JobRequirementEvidence[]>(revision.Evidence, OutputValidator.Json)!;
        foreach (var item in evidence) Assert.Equal(item.Quote, text[item.StartOffset!.Value..item.EndOffset!.Value]);
        Assert.True(JobRevisionFactory.TryConfirm(revision, Guid.NewGuid(), 2,
            new(1, data.Requirements.Select(x => new JobRequirementSelection(x.RequirementId, x.Importance)).ToArray(), []), DateTimeOffset.UtcNow, out var confirmed, out _));
        Assert.Contains("any_of", confirmed!.StructuredData);
    }

    [Fact]
    public void LabelledSyntheticJobFixturesRemainGroupedAndMatchStructuralAnnotations()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "project-docs/evaluation/jd-atomic-smoke.json"))) root = root.Parent;
        Assert.NotNull(root);
        using var labels = JsonDocument.Parse(File.ReadAllText(Path.Combine(root.FullName, "project-docs/evaluation/jd-atomic-smoke.json")));
        Assert.True(labels.RootElement.GetProperty("synthetic").GetBoolean());
        foreach (var item in labels.RootElement.GetProperty("documents").EnumerateArray())
        {
            Assert.True(JobRevisionFactory.TryExtract(Guid.NewGuid(), null, 1, item.GetProperty("text").GetString()!, DateTimeOffset.UtcNow, out var revision, out _));
            var data = JsonSerializer.Deserialize<JobStructuredRequirements>(revision!.StructuredData, OutputValidator.Json)!;
            Assert.Equal(item.GetProperty("expectedCount").GetInt32(), data.Requirements.Count);
            Assert.Equal(item.GetProperty("requiredCount").GetInt32(), data.Requirements.Count(x => x.Importance == "required"));
            Assert.Equal(item.GetProperty("optionalCount").GetInt32(), data.Requirements.Count(x => x.Importance == "optional"));
            Assert.Equal(item.GetProperty("anyOfCount").GetInt32(), data.Requirements.Count(x => x.Relation == "any_of"));
            if (item.GetProperty("minimumYears").ValueKind == JsonValueKind.Number)
                Assert.Contains(data.Requirements, x => x.MinimumYears == item.GetProperty("minimumYears").GetDecimal());
        }
    }

    [Theory]
    [InlineData(false, "ai_output_invalid")]
    [InlineData(true, "provider_timeout")]
    public async Task FullContractErrorsRetryOnlyOnceWithoutPublishingRevision(bool timeout, string code)
    {
        var (owner, candidate) = await fixture.User();
        var uploaded = await FoundationFixture.Upload(owner, FoundationFixture.Docx(Synthetic));
        using var fault = new TestApp(fixture.Connection, Path.Combine(Path.GetTempPath(), new Npgsql.NpgsqlConnectionStringBuilder(fixture.Connection).Database!), new FullFaultClient(timeout));
        using var scope = fault.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<JobService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var job = await service.Enqueue(candidate, new(uploaded.GetProperty("versionId").GetGuid(), Guid.NewGuid(), FullCvExtraction.Contract), default);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_jobs SET available_at = now() + interval '1 day' WHERE status = 'queued' AND id <> {job.Id}");
        try
        {
            for (var i = 0; i < 2; i++)
            {
                db.ChangeTracker.Clear();
                var claim = await service.Claim("full-fault", default); Assert.NotNull(claim); Assert.Equal(job.Id, claim.Id);
                await JobWorker.Process(scope.ServiceProvider, job.Id, "full-fault", claim.LeaseGeneration, default);
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ai_jobs SET available_at = now() WHERE id = {job.Id}");
            }
            db.ChangeTracker.Clear();
            var saved = await db.AiJobs.SingleAsync(x => x.Id == job.Id);
            Assert.Equal("failed", saved.Status); Assert.Equal(code, saved.ErrorCode); Assert.Null(saved.OutputJson);
            Assert.False(await db.ResumeDataRevisions.AnyAsync(x => x.AiJobId == job.Id));
            Assert.Equal(2, await db.ExternalCallAttempts.CountAsync(x => x.AiJobId == job.Id));
            Assert.Equal(timeout ? 2m : 0m, (await db.BudgetReservations.SingleAsync(x => x.AiJobId == job.Id)).ReservedRemaining);
        }
        finally { await db.Database.ExecuteSqlRawAsync("UPDATE ai_jobs SET available_at = now() WHERE status = 'queued'"); }
    }

    [Fact]
    public async Task TruncatedValidJsonSettlesKnownUsageWithoutRetryOrPublishingRevision()
    {
        var (owner, candidate) = await fixture.User();
        var uploaded = await FoundationFixture.Upload(owner, FoundationFixture.Docx(Synthetic));
        using var fault = new TestApp(fixture.Connection, Path.Combine(Path.GetTempPath(), new Npgsql.NpgsqlConnectionStringBuilder(fixture.Connection).Database!), new TruncatedClient());
        using var scope = fault.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<JobService>(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var job = await service.Enqueue(candidate, new(uploaded.GetProperty("versionId").GetGuid(), Guid.NewGuid(), FullCvExtraction.Contract), default);
        var claimed = await service.Claim("truncated", default); Assert.NotNull(claimed); Assert.Equal(job.Id, claimed.Id);
        await JobWorker.Process(scope.ServiceProvider, job.Id, "truncated", claimed.LeaseGeneration, default);
        db.ChangeTracker.Clear();
        var saved = await db.AiJobs.SingleAsync(x => x.Id == job.Id);
        Assert.Equal("failed", saved.Status); Assert.Equal("ai_output_truncated", saved.ErrorCode); Assert.Null(saved.OutputJson);
        Assert.False(await db.ResumeDataRevisions.AnyAsync(x => x.AiJobId == job.Id));
        var attempt = await db.ExternalCallAttempts.SingleAsync(x => x.AiJobId == job.Id);
        Assert.Equal("failed", attempt.Status); Assert.NotNull(attempt.ReconciledAt);
        Assert.Equal(0m, (await db.BudgetReservations.SingleAsync(x => x.AiJobId == job.Id)).ReservedRemaining);
    }

    private sealed class TruncatedClient : FakeLlmClient
    {
        public override Task<LlmReply> ExtractAsync(ParsedDocument document, MethodParameters parameters, CancellationToken ct)
            => Task.FromResult(new LlmReply("{\"fields\":[]}", 0, 0, "length"));
    }

    private sealed class FullFaultClient(bool timeout) : FakeLlmClient
    {
        public override Task<LlmReply> ExtractAsync(ParsedDocument document, MethodParameters parameters, CancellationToken ct)
            => timeout ? throw new TaskCanceledException() : Task.FromResult(new LlmReply("{\"fields\":[{\"path\":\"summary\",\"value\":\"Invented fact\",\"quote\":\"React\",\"evidenceRef\":\"paragraph-1\"}]}", 0, 0));
    }
}
