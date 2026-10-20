using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CareerGap.Api.Ai;
using CareerGap.Api.Files;

namespace CareerGap.Api.Matching;

public sealed record CvEvaluationLabel(string Path, string? Value, string Status, string? Quote, string ReviewStatus);
public sealed record CvEvaluationDocument(string Id, string GroupId, string Language, string Format, string Layout,
    string File, string Sha256, bool Synthetic, bool LocalEvaluationAllowed, string RightsEvidenceRef, CvEvaluationLabel[] Labels);
public sealed record CvCaptureDataset(int SchemaVersion, string DatasetId, string Split, string? SplitLockedAtUtc,
    CvEvaluationDocument[] Documents);
public sealed record CvCapturedDocument(string Id, string SourceSha256, string? RawOutput, string? FinishReason,
    string? ErrorCode, long? InputTokens, long? OutputTokens, decimal? Cost, long? ElapsedMs, string? SourceJobId = null);
public sealed record CvCapturedRun(int SchemaVersion, string DatasetSha256, string Contract, string Parser,
    string Provider, string Model, string Method, string PromptSha256, JsonElement Parameters,
    bool BeforeUserCorrection, string Unit, CvCapturedDocument[] Documents);

/// <summary>Offline replay only. No host, database, HTTP client, dispatch, or provider secrets.</summary>
public static class CvCaptureEvaluation
{
    public const string Protocol = "cv-capture-exact-path-value-v1";
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool Digest(string? value) => value is not null && Regex.IsMatch(value, "^[a-f0-9]{64}$");
    private static bool Identity(string? value) => value is not null && Regex.IsMatch(value, "^[a-zA-Z0-9_-]{1,100}$");
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool valid, string code)
    { if (!valid) throw new InvalidDataException(code); }

    private static string PrivatePath(string path, string privateRoot)
    {
        var absolute = Path.GetFullPath(path);
        Require(absolute.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase), "private_path_required");
        // Prevent a junction/symlink inside .local from bypassing the explicit private path boundary.
        for (var current = new FileInfo(absolute) as FileSystemInfo; current is not null; current = current switch
        { FileInfo f => f.Directory, DirectoryInfo d => d.Parent, _ => null })
        {
            if (current.Exists) Require((current.Attributes & FileAttributes.ReparsePoint) == 0, "linked_path_not_allowed");
            if (current.FullName.TrimEnd(Path.DirectorySeparatorChar).Equals(privateRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) break;
        }
        return absolute;
    }

    private static async Task<byte[]> Read(string path, long limit)
    {
        Require(new FileInfo(path).Length <= limit, "evaluation_input_limit");
        return await File.ReadAllBytesAsync(path);
    }

    public static async Task Run(string datasetPath, string runPath, string outputPath)
    {
        var repository = new DirectoryInfo(Environment.CurrentDirectory);
        while (repository is not null && !Directory.Exists(Path.Combine(repository.FullName, "project-docs"))) repository = repository.Parent;
        Require(repository is not null, "workspace_required");
        var privateRoot = Path.GetFullPath(Path.Combine(repository!.FullName, ".local")) + Path.DirectorySeparatorChar;
        datasetPath = PrivatePath(datasetPath, privateRoot); runPath = PrivatePath(runPath, privateRoot);
        outputPath = PrivatePath(outputPath, privateRoot);
        Require(!File.Exists(outputPath), "evaluation_output_already_exists");
        var datasetBytes = await Read(datasetPath, 8_000_000);
        var runBytes = await Read(runPath, 16_000_000);
        var dataset = JsonSerializer.Deserialize<CvCaptureDataset>(datasetBytes, OutputValidator.Json);
        var run = JsonSerializer.Deserialize<CvCapturedRun>(runBytes, OutputValidator.Json);
        Require(dataset is { SchemaVersion: 1, Documents.Length: > 0 and <= 100 } && Identity(dataset.DatasetId) && dataset.Documents.All(x => x is not null), "evaluation_dataset_invalid");
        Require(dataset!.Split is "smoke" or "demo" or "tuning" or "final", "evaluation_split_invalid");
        Require(dataset.Split is not ("tuning" or "final") || DateTimeOffset.TryParse(dataset.SplitLockedAtUtc, out _), "split_lock_required");
        Require(run is { SchemaVersion: 1, Documents: not null, BeforeUserCorrection: true } && run.Documents.All(x => x is not null) &&
            run.DatasetSha256 == Hash(datasetBytes) && run.Contract == FullCvExtraction.Contract && run.Parser == DocumentParser.Version &&
            run.Provider is "Fake" or "DeepSeek" && !string.IsNullOrWhiteSpace(run.Model) && !string.IsNullOrWhiteSpace(run.Method) &&
            Digest(run.PromptSha256) && run.Parameters.ValueKind == JsonValueKind.Object &&
            run.Unit == (run.Provider == "Fake" ? "test_credit" : "usd"), "capture_metadata_invalid");
        var ids = dataset.Documents.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        Require(ids.Count == dataset.Documents.Length && run!.Documents.Length <= dataset.Documents.Length &&
            run.Documents.All(x => ids.Contains(x.Id)) && run.Documents.Select(x => x.Id).Distinct().Count() == run.Documents.Length, "capture_ids_invalid");
        var captures = run.Documents.ToDictionary(x => x.Id);
        var results = new List<JsonElement>();
        var inputHashes = new Dictionary<string, string>();
        foreach (var item in dataset.Documents)
        {
            Require(Identity(item.Id) && Identity(item.GroupId) && Digest(item.Sha256) && item.Language is "vi" or "en" or "mixed" or "unknown" &&
                item.Format is "pdf" or "docx" && !string.IsNullOrWhiteSpace(item.Layout), "document_metadata_invalid");
            Require(item.LocalEvaluationAllowed && !string.IsNullOrWhiteSpace(item.RightsEvidenceRef), "local_evaluation_rights_required");
            Require(!inputHashes.TryGetValue(item.Sha256, out var group) || group == item.GroupId, "duplicate_source_group_mismatch");
            inputHashes[item.Sha256] = item.GroupId;
            Require(item.Labels is not null && item.Labels.Length <= 180 && item.Labels.All(x => x is not null) && item.Labels.Select(x => x.Path).Distinct().Count() == item.Labels.Length,
                "field_labels_invalid");
            foreach (var label in item.Labels!)
                Require(FullCvExtraction.ValidField(new(label.Path, "x")) && label.Status is "present" or "absent" or "unreadable" or "ambiguous" &&
                    label.ReviewStatus is "pending" or "self_reviewed" or "reviewed" && (label.Status != "present" ||
                    FullCvExtraction.ValidField(new(label.Path, label.Value!)) && !string.IsNullOrWhiteSpace(label.Quote) && label.Quote.Contains(label.Value!, StringComparison.Ordinal)), "field_label_invalid");
            var file = PrivatePath(Path.Combine(Path.GetDirectoryName(datasetPath)!, item.File), privateRoot);
            Require(file != outputPath && Path.GetExtension(file).Equals("." + item.Format, StringComparison.OrdinalIgnoreCase), "source_path_invalid");
            var bytes = await Read(file, DocumentParser.MaxBytes);
            Require(Hash(bytes) == item.Sha256, "source_hash_mismatch");
            var capture = captures.GetValueOrDefault(item.Id);
            Require(capture is null || capture.SourceSha256 == item.Sha256 && capture.InputTokens is not < 0 && capture.OutputTokens is not < 0 &&
                capture.Cost is not < 0 && capture.ElapsedMs is not < 0 && capture.RawOutput?.Length is not > 128000 &&
                (capture.ErrorCode is null || Regex.IsMatch(capture.ErrorCode, "^[a-z0-9_]{1,80}$")), "capture_document_invalid");
            string? error = null;
            var errorStage = "capture";
            ParsedDocument? parsed = null; FullCvOutput? extracted = null;
            try
            {
                errorStage = "parser";
                parsed = new DocumentParser().Parse(bytes, Path.GetFileName(file), default);
                if (capture is null) { error = "capture_missing"; errorStage = "capture"; }
                else if (capture.ErrorCode is not null) { error = capture.ErrorCode; errorStage = "provider"; }
                else
                {
                    errorStage = "completion";
                    CompletionValidator.Validate(new(capture.RawOutput ?? "", capture.InputTokens, capture.OutputTokens, capture.FinishReason));
                    errorStage = "schema_or_evidence";
                    extracted = FullCvExtraction.Validate(capture.RawOutput!, parsed);
                }
            }
            catch (DocumentError failure) { error = failure.Code; }
            catch (ProviderError failure) { error = failure.Code; }
            var reviewed = item.Labels.Where(x => x.ReviewStatus != "pending" && x.Status is "present" or "absent").ToArray();
            var present = reviewed.Where(x => x.Status == "present").ToArray();
            var predictions = extracted?.Fields.ToDictionary(x => x.Path);
            var tp = 0; var fp = 0; var fn = 0; var sourceDisagreements = 0;
            var fields = new List<object>();
            foreach (var label in reviewed)
            {
                var prediction = predictions?.GetValueOrDefault(label.Path);
                var correct = prediction is not null && label.Status == "present" && prediction.Value == label.Value;
                if (extracted is not null)
                {
                    if (correct) tp++;
                    else { if (prediction is not null) fp++; if (label.Status == "present") fn++; }
                    if (correct && parsed is not null && !parsed.Segments.Single(x => x.Id == prediction!.EvidenceRef).Text.Contains(label.Quote!, StringComparison.Ordinal)) sourceDisagreements++;
                }
                fields.Add(new { label.Path, result = extracted is null ? "not_scored" : correct ? "correct" : prediction is null ?
                    (label.Status == "absent" ? "correct_absent" : "missing") : label.Status == "absent" ? "unexpected" : "wrong_value" });
            }
            double? precision = extracted is null || tp + fp == 0 ? null : (double)tp / (tp + fp);
            double? recall = extracted is null || tp + fn == 0 ? null : (double)tp / (tp + fn);
            double? f1 = extracted is null || 2 * tp + fp + fn == 0 ? null : (double)(2 * tp) / (2 * tp + fp + fn);
            results.Add(JsonSerializer.SerializeToElement(new { item.Id, item.GroupId, item.Language, item.Format, item.Layout, item.Synthetic,
                error, errorStage = error is null ? null : errorStage, scored = extracted is not null,
                tp = extracted is null ? (int?)null : tp, fp = extracted is null ? (int?)null : fp, fn = extracted is null ? (int?)null : fn,
                precision, recall, f1, sourceDisagreements = extracted is null ? (int?)null : sourceDisagreements,
                reviewedLabels = reviewed.Length, pendingLabels = item.Labels.Count(x => x.ReviewStatus == "pending"),
                ambiguousOrUnreadableLabels = item.Labels.Count(x => x.Status is "ambiguous" or "unreadable"),
                unscoredPredictions = extracted?.Fields.Count(x => !reviewed.Any(l => l.Path == x.Path)),
                parserExpectedValues = present.Length, parserRecoveredValues = parsed is null ? (int?)null : present.Count(x => parsed.Segments.Any(s => s.Text.Contains(x.Value!, StringComparison.Ordinal))),
                capture?.InputTokens, capture?.OutputTokens, capture?.Cost, capture?.ElapsedMs,
                rawOutputSha256 = capture?.RawOutput is null ? null : Hash(Encoding.UTF8.GetBytes(capture.RawOutput)), fields
            }, OutputValidator.Json));
        }
        object Aggregate(IEnumerable<JsonElement> subset)
        {
            var rows = subset.ToArray(); var scored = rows.Where(x => x.GetProperty("scored").GetBoolean()).ToArray();
            var tp = scored.Sum(x => x.GetProperty("tp").GetInt32()); var fp = scored.Sum(x => x.GetProperty("fp").GetInt32()); var fn = scored.Sum(x => x.GetProperty("fn").GetInt32());
            return new { documents = rows.Length, groups = rows.Select(x => x.GetProperty("groupId").GetString()).Distinct().Count(), scoredDocuments = scored.Length,
                failedOrMissingDocuments = rows.Length - scored.Length, tp, fp, fn,
                precision = tp + fp == 0 ? (double?)null : (double)tp / (tp + fp), recall = tp + fn == 0 ? (double?)null : (double)tp / (tp + fn),
                f1 = 2 * tp + fp + fn == 0 ? (double?)null : (double)(2 * tp) / (2 * tp + fp + fn) };
        }
        var report = new { protocol = Protocol, dataset.DatasetId, dataset.Split, ranAtUtc = DateTimeOffset.UtcNow,
            datasetSha256 = Hash(datasetBytes), captureSha256 = Hash(runBytes), sourceAssemblySha256 = Hash(await File.ReadAllBytesAsync(typeof(CvCaptureEvaluation).Assembly.Location)),
            sourceRevision = await MatchingEvaluationRunner.ReadGitRevisionAsync(), sourceState = "working_tree_use_source_manifest",
            run.Provider, run.Model, run.Method, run.PromptSha256, run.Parameters, run.Parser, run.Contract, run.Unit,
            beforeUserCorrection = true, providerCalled = false, paidCalls = 0, replayNotNewLatencyOrUsage = true,
            syntheticOnly = dataset.Documents.All(x => x.Synthetic), independentQualityAcceptance = false,
            rightsAndLabelsAreDeclarationsRequiringHumanReview = true, groupedVariantsAreNotIndependentSamples = true,
            overall = Aggregate(results), byLanguage = results.GroupBy(x => x.GetProperty("language").GetString()!).ToDictionary(x => x.Key, x => Aggregate(x)),
            byFormat = results.GroupBy(x => x.GetProperty("format").GetString()!).ToDictionary(x => x.Key, x => Aggregate(x)),
            byFieldGroup = results.SelectMany(row => row.GetProperty("fields").EnumerateArray().Select(field => new { row, field }))
                .GroupBy(x => Regex.Split(x.field.GetProperty("path").GetString()!, "[\\[.]")[0])
                .ToDictionary(g => g.Key, g => g.GroupBy(x => x.field.GetProperty("result").GetString()!).ToDictionary(x => x.Key, x => x.Count())),
            documents = results };
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write);
        await JsonSerializer.SerializeAsync(output, report, MatchingEvaluationRunner.JsonOptions);
        Console.WriteLine($"Offline extraction replay: {results.Count} documents; no provider call; private aggregate report written.");
    }
}
