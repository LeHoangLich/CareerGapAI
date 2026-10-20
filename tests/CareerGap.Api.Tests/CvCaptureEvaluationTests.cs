using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Files;
using CareerGap.Api.Matching;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class CvCaptureEvaluationTests : IDisposable
{
    private readonly string folder;
    private readonly string datasetPath;
    private readonly string capturePath;
    private readonly string outputPath;
    private CvCaptureDataset dataset;
    private CvCapturedRun run;
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    public CvCaptureEvaluationTests()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "project-docs"))) root = root.Parent;
        folder = Path.Combine(root!.FullName, ".local", "evaluation", "capture-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        datasetPath = Path.Combine(folder, "dataset.json"); capturePath = Path.Combine(folder, "captures.json"); outputPath = Path.Combine(folder, "report.json");
        var bytes = FoundationFixture.Docx("Name: Synthetic Candidate\nSkills: React");
        File.WriteAllBytes(Path.Combine(folder, "synthetic.docx"), bytes);
        dataset = new(1, "synthetic-replay", "smoke", null,
            [new("cv-001", "person-001", "vi", "docx", "paragraphs", "synthetic.docx", Hash(bytes), true, true, "synthetic-owned",
                [new("displayName", "Synthetic Candidate", "present", "Synthetic Candidate", "self_reviewed"),
                 new("skills[0]", "React", "present", "React", "self_reviewed"), new("phone", null, "absent", null, "self_reviewed"),
                 new("skills[1]", null, "ambiguous", null, "self_reviewed"), new("summary", null, "absent", null, "pending")])]);
        var parsed = new DocumentParser().Parse(bytes, "synthetic.docx", default);
        var raw = JsonSerializer.Serialize(FullCvExtraction.FakeExtract(parsed), OutputValidator.Json);
        run = new(1, "", FullCvExtraction.Contract, DocumentParser.Version, "Fake", "synthetic-model", "synthetic-method",
            Hash(Encoding.UTF8.GetBytes(FullCvExtraction.Prompt)), JsonSerializer.SerializeToElement(new { maxOutputTokens = 2048 }), true, "test_credit",
            [new("cv-001", Hash(bytes), raw, "stop", null, 0, 0, 0, 1)]);
    }

    private async Task<JsonElement> Evaluate()
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(dataset, OutputValidator.Json); await File.WriteAllBytesAsync(datasetPath, bytes);
        run = run with { DatasetSha256 = Hash(bytes) };
        await File.WriteAllTextAsync(capturePath, JsonSerializer.Serialize(run, OutputValidator.Json));
        await CvCaptureEvaluation.Run(datasetPath, capturePath, outputPath);
        return JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(outputPath));
    }

    [Fact]
    public async Task ReplayScoresOriginalOutputAndReportsGroupsCoverageWithoutRawValues()
    {
        var report = await Evaluate();
        Assert.False(report.GetProperty("providerCalled").GetBoolean()); Assert.True(report.GetProperty("replayNotNewLatencyOrUsage").GetBoolean());
        Assert.Equal(2, report.GetProperty("overall").GetProperty("tp").GetInt32());
        Assert.Equal(1, report.GetProperty("overall").GetProperty("groups").GetInt32());
        Assert.Equal(1, report.GetProperty("documents")[0].GetProperty("pendingLabels").GetInt32());
        Assert.DoesNotContain("Synthetic Candidate", await File.ReadAllTextAsync(outputPath));
        Assert.Equal(1, report.GetProperty("byFormat").GetProperty("docx").GetProperty("documents").GetInt32());
    }

    [Theory]
    [InlineData("length", "{\"fields\":[]}", "ai_output_truncated")]
    [InlineData("stop", "{bad", "ai_output_invalid")]
    [InlineData("stop", "{\"fields\":[{\"path\":\"summary\",\"value\":\"invented\",\"evidenceRef\":\"paragraph-1\",\"quote\":\"invented\"}]}", "ai_output_invalid")]
    public async Task InvalidOutputIsUnscoredAndPreservesFailureInsteadOfFakeZero(string finish, string raw, string code)
    {
        run = run with { Documents = [run.Documents[0] with { RawOutput = raw, FinishReason = finish }] };
        var report = await Evaluate(); var doc = report.GetProperty("documents")[0];
        Assert.Equal(code, doc.GetProperty("error").GetString()); Assert.Equal(JsonValueKind.Null, doc.GetProperty("tp").ValueKind);
        Assert.Equal(0, report.GetProperty("overall").GetProperty("scoredDocuments").GetInt32());
        Assert.Equal(JsonValueKind.Null, report.GetProperty("overall").GetProperty("f1").ValueKind);
    }

    [Fact]
    public async Task ValidEmptyOutputWithReadableGoldIsAnActualZeroRecall()
    {
        run = run with { Documents = [run.Documents[0] with { RawOutput = "{\"fields\":[]}" }] };
        var report = await Evaluate(); var doc = report.GetProperty("documents")[0];
        Assert.Equal(2, doc.GetProperty("fn").GetInt32()); Assert.Equal(0, doc.GetProperty("f1").GetDouble());
    }

    [Fact]
    public async Task MissingCaptureHasSeparateStatusAndDoesNotPretendModelReturnedEmptyOutput()
    {
        run = run with { Documents = [] };
        var report = await Evaluate(); var doc = report.GetProperty("documents")[0];
        Assert.Equal("capture_missing", doc.GetProperty("error").GetString()); Assert.Equal(JsonValueKind.Null, doc.GetProperty("recall").ValueKind);
    }

    [Theory]
    [InlineData("rights")]
    [InlineData("source_hash")]
    [InlineData("corrected")]
    [InlineData("split")]
    [InlineData("duplicate")]
    [InlineData("parser")]
    public async Task ReplayRejectsWrongRightsHashesEditedOutputLeaksAndDuplicateCaptures(string fault)
    {
        if (fault == "rights") dataset = dataset with { Documents = [dataset.Documents[0] with { LocalEvaluationAllowed = false }] };
        if (fault == "source_hash") dataset = dataset with { Documents = [dataset.Documents[0] with { Sha256 = new string('a', 64) }] };
        if (fault == "corrected") run = run with { BeforeUserCorrection = false };
        if (fault == "split") dataset = dataset with { Split = "final" };
        if (fault == "duplicate") run = run with { Documents = [run.Documents[0], run.Documents[0]] };
        if (fault == "parser") run = run with { Parser = "different-parser" };
        await Assert.ThrowsAsync<InvalidDataException>(Evaluate);
        Assert.False(File.Exists(outputPath));
    }

    public void Dispose()
    {
        // Only delete this test's resolved UUID directory inside repository .local/evaluation.
        var directory = new DirectoryInfo(folder);
        if (directory.Name.StartsWith("capture-test-", StringComparison.Ordinal) && directory.Parent?.Name == "evaluation" && directory.Parent.Parent?.Name == ".local")
            directory.Delete(true);
    }
}
