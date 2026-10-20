using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Files;
using CareerGap.Api.Recruitment;

namespace CareerGap.Api.Matching;

public sealed record ExtractionFixture(string Id, string GroupId, string Language, string Format, string Layout,
    string File, string Sha256, CvField[] Expected, string? ExpectedError);
public sealed record ExtractionDataset(string DatasetId, bool Synthetic, string Split, ExtractionFixture[] Documents);

public static class ExtractionEvaluation
{
    public static async Task Run(string datasetPath, string outputPath)
    {
        var raw = await File.ReadAllBytesAsync(datasetPath);
        var dataset = JsonSerializer.Deserialize<ExtractionDataset>(raw, OutputValidator.Json) ?? throw new InvalidDataException("evaluation_dataset_invalid");
        if (!dataset.Synthetic || dataset.Split != "smoke" || dataset.Documents is null || dataset.Documents.Length is < 1 or > 100 ||
            dataset.Documents.Select(x => x.Id).Distinct().Count() != dataset.Documents.Length) throw new InvalidDataException("synthetic_smoke_required");
        if (dataset.Documents.Any(x => !System.Text.RegularExpressions.Regex.IsMatch(x.Id, @"^[a-zA-Z0-9-]{1,100}$") || x.Expected is null || x.Expected.Any(field => !FullCvExtraction.ValidField(field))))
            throw new InvalidDataException("fixture_metadata_invalid");
        var repository = new DirectoryInfo(Environment.CurrentDirectory);
        while (repository is not null && !Directory.Exists(Path.Combine(repository.FullName, "project-docs"))) repository = repository.Parent;
        if (repository is null) throw new InvalidDataException("workspace_required");
        var privateRoot = Path.GetFullPath(Path.Combine(repository.FullName, ".local")) + Path.DirectorySeparatorChar;
        outputPath = Path.GetFullPath(outputPath);
        if (!outputPath.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase) || outputPath == Path.GetFullPath(datasetPath))
            throw new InvalidDataException("private_output_required");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        var results = new List<object>();
        foreach (var item in dataset.Documents)
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(datasetPath))! + Path.DirectorySeparatorChar;
            var sourcePath = Path.GetFullPath(Path.Combine(directory, item.File));
            if (!sourcePath.StartsWith(directory, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("fixture_path_invalid");
            var bytes = await File.ReadAllBytesAsync(sourcePath);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != item.Sha256) throw new InvalidDataException("fixture_hash_mismatch");
            var watch = Stopwatch.StartNew();
            try
            {
                var parsed = new DocumentParser().Parse(bytes, item.File, default);
                var readable = item.Expected.Count(field => parsed.Segments.Any(segment => segment.Text.Contains(field.Value, StringComparison.Ordinal)));
                var extracted = FullCvExtraction.FakeExtract(parsed);
                FullCvExtraction.Validate(JsonSerializer.Serialize(extracted, OutputValidator.Json), parsed);
                var truth = item.Expected.ToDictionary(x => x.Path, x => x.Value, StringComparer.Ordinal);
                var tp = extracted.Fields.Count(x => truth.TryGetValue(x.Path, out var value) && value == x.Value);
                var fp = extracted.Fields.Count - tp;
                var fn = item.Expected.Length - tp;
                double? precision = tp + fp == 0 ? null : (double)tp / (tp + fp);
                double? recall = tp + fn == 0 ? null : (double)tp / (tp + fn);
                var output = Path.GetFileNameWithoutExtension(outputPath) + "-" + item.Id + "-raw.json";
                await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(outputPath)!, output), JsonSerializer.Serialize(new { parsed, extracted }, MatchingEvaluationRunner.JsonOptions));
                results.Add(new { item.Id, item.GroupId, item.Language, item.Format, item.Layout, error = (string?)null,
                    expectedErrorMatched = item.ExpectedError is null, parserExpectedValues = item.Expected.Length, parserRecoveredValues = readable,
                    tp, fp, fn, precision, recall, f1 = precision + recall > 0 ? 2 * precision * recall / (precision + recall) : null,
                    elapsedMs = watch.ElapsedMilliseconds, beforeUserCorrection = true, rawOutput = output });
            }
            catch (DocumentError error)
            {
                results.Add(new { item.Id, item.GroupId, item.Language, item.Format, item.Layout, error = error.Code,
                    expectedErrorMatched = error.Code == item.ExpectedError, elapsedMs = watch.ElapsedMilliseconds,
                    beforeUserCorrection = true });
            }
        }
        var manifest = new { dataset.DatasetId, dataset.Split, syntheticOnly = true, qualityEvidence = false,
            ranAtUtc = DateTimeOffset.UtcNow, parser = DocumentParser.Version, method = FullCvExtraction.Contract + ":fake-explicit-labels-v1",
            provider = "Fake", unit = "test_credit", paidCalls = 0, inputSha256 = Convert.ToHexStringLower(SHA256.HashData(raw)),
            promptSha256 = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(FullCvExtraction.Prompt))),
            sourceAssemblySha256 = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(typeof(ExtractionEvaluation).Assembly.Location))),
            sourceRevision = await MatchingEvaluationRunner.ReadGitRevisionAsync(), sourceState = "working_tree_use_report_and_source_hashes",
            documents = results, groupedVariantsAreNotIndependentSamples = true };
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(manifest, MatchingEvaluationRunner.JsonOptions));
        Console.WriteLine($"Synthetic extraction smoke: {results.Count} files; raw outputs private; no quality claim or provider call.");
    }
}
