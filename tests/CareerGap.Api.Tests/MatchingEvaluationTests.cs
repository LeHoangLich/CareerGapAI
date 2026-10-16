using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using CareerGap.Api.Matching;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class MatchingEvaluationTests
{
    [Fact]
    public void ScorerFoldsVietnameseAccentsAndKeepsTechnicalTokens()
    {
        var options = new KeywordBaselineOptions { StopWords = ["va", "the"] };
        var score = KeywordBaselineScorer.Score("KỸ năng .NET C# C++ Node.js CI/CD PostgreSQL",
            new("ky NANG", ".NET C# C++ Node.js CI/CD PostgreSQL", ""), options);

        Assert.Equal(1d, score.RetrievalScore);
        Assert.Contains(".net", score.MatchedTerms);
        Assert.Contains("c#", score.MatchedTerms);
        Assert.Contains("c++", score.MatchedTerms);
        Assert.Contains("node.js", score.MatchedTerms);
        Assert.Contains("ci/cd", score.MatchedTerms);
    }

    [Fact]
    public void ScorerUsesFieldWeightsAndDoesNotInventCrossLanguageSynonyms()
    {
        var options = new KeywordBaselineOptions();
        var weighted = KeywordBaselineScorer.Score("backend C#", new("backend", "C# PostgreSQL", "React"), options);
        Assert.Equal(5d / 8d, weighted.RetrievalScore);

        var unrelated = KeywordBaselineScorer.Score("PostgreSQL", new("database", "", ""), options);
        Assert.Equal(0d, unrelated.RetrievalScore);
        Assert.Equal(0, unrelated.MatchedTermCount);
        var stopWordOnlyOptions = new KeywordBaselineOptions { StopWords = ["the", "and", "va"] };
        Assert.Null(KeywordBaselineScorer.Score("", new("the and va", "", ""), stopWordOnlyOptions).RetrievalScore);
    }

    [Fact]
    public void ConfirmedRevisionDiagnosticKeepsBaselineSeparateAndExcludesPrivateStatementsFromPublicResumeScope()
    {
        var candidateId = Guid.NewGuid();
        var recruiterId = Guid.NewGuid();
        var resume = new ResumeDataRevision
        {
            CandidateId = candidateId,
            ResumeVersionId = Guid.NewGuid(),
            RevisionNo = 2,
            Origin = "user_confirmed",
            SchemaVersion = 1,
            StructuredData = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                skills = new[] { new { name = "C#" }, new { name = "Go" } }
            }, MatchingEvaluationRunner.JsonOptions),
            SourceText = JsonSerializer.Serialize(new[] { new SourceSegment("segment-1", "C# backend") },
                MatchingEvaluationRunner.JsonOptions),
            Evidence = JsonSerializer.Serialize(new object[]
            {
                new { skillName = "C#", sourceType = "document_span", segmentId = "segment-1", startOffset = 0, endOffset = 2, quote = "C#", statement = (string?)null },
                new { skillName = "Go", sourceType = "user_statement", segmentId = (string?)null, startOffset = (int?)null, endOffset = (int?)null, quote = (string?)null, statement = "Go" }
            }, MatchingEvaluationRunner.JsonOptions),
            DataHash = new string('a', 64),
            ParserVersion = "synthetic-v1",
            ConfirmedBy = candidateId,
            ConfirmedAt = DateTimeOffset.UtcNow
        };
        const string sourceRequirements = "Must have C#\nGo is preferred";
        var requiredQuote = "Must have C#";
        var optionalQuote = "Go is preferred";
        var job = new JobDataRevision
        {
            JobVersionId = Guid.NewGuid(),
            RevisionNo = 2,
            Origin = "user_confirmed",
            SchemaVersion = 1,
            StructuredData = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                requirements = new[]
                {
                    new { requirementId = "source-0001", text = requiredQuote, importance = "required", sourceType = "document_text" },
                    new { requirementId = "source-0002", text = optionalQuote, importance = "optional", sourceType = "document_text" }
                }
            }, MatchingEvaluationRunner.JsonOptions),
            SourceText = JsonSerializer.Serialize(new { requirementsText = sourceRequirements }, MatchingEvaluationRunner.JsonOptions),
            Evidence = JsonSerializer.Serialize(new object[]
            {
                new { requirementId = "source-0001", sourceType = "document_text", startOffset = sourceRequirements.IndexOf(requiredQuote, StringComparison.Ordinal), endOffset = sourceRequirements.IndexOf(requiredQuote, StringComparison.Ordinal) + requiredQuote.Length, quote = requiredQuote, statement = (string?)null },
                new { requirementId = "source-0002", sourceType = "document_text", startOffset = sourceRequirements.IndexOf(optionalQuote, StringComparison.Ordinal), endOffset = sourceRequirements.IndexOf(optionalQuote, StringComparison.Ordinal) + optionalQuote.Length, quote = optionalQuote, statement = (string?)null }
            }, MatchingEvaluationRunner.JsonOptions),
            DataHash = new string('b', 64),
            ParserVersion = "synthetic-v1",
            ConfirmedBy = recruiterId,
            ConfirmedAt = DateTimeOffset.UtcNow
        };

        var options = new KeywordBaselineOptions();
        var candidateView = ConfirmedRevisionMatcher.Evaluate(resume, job, options, publicResumeScope: false);
        Assert.Equal("ready", candidateView.Status);
        Assert.Equal(2, candidateView.EligibleSkillCount);
        Assert.Equal(2, candidateView.MatchedRequirementCount);
        Assert.Equal(1, candidateView.MatchedRequiredRequirementCount);

        var publicRecruiterView = ConfirmedRevisionMatcher.Evaluate(resume, job, options, publicResumeScope: true);
        Assert.Equal("ready", publicRecruiterView.Status);
        Assert.Equal(1, publicRecruiterView.EligibleSkillCount);
        Assert.Equal(1, publicRecruiterView.MatchedRequirementCount);
        Assert.Equal(1, publicRecruiterView.MatchedRequiredRequirementCount);

        var missingResume = ConfirmedRevisionMatcher.Evaluate(null, job, options, publicResumeScope: false);
        Assert.Equal("resume_revision_unconfirmed", missingResume.Status);
        Assert.Null(missingResume.MatchedRequirementCount);
    }

    [Fact]
    public async Task EvaluationLeavesUnlabeledPairsUnknownAndReportsCoverage()
    {
        var dataset = new MatchingEvaluationDataset
        {
            DatasetId = "synthetic-smoke",
            Split = "smoke",
            Resumes =
            [
                new MatchingEvaluationResume("resume-a", "person-a", "en", "React TypeScript frontend")
                {
                    ConfirmedRevision = new("user_confirmed", 1, true,
                        [new("React", "document_span"), new("Go", "user_statement")])
                },
                new MatchingEvaluationResume("resume-b", "person-b", "en", "Docker operations")
                {
                    ConfirmedRevision = new("ai_extracted", 1, false, [new("Docker", "document_span")])
                }
            ],
            Jobs =
            [
                new MatchingEvaluationJob("job-a", "job-a", "en", "React", "TypeScript", "Frontend role")
                {
                    ConfirmedRevision = new("user_confirmed", 1, true,
                        [new("Go", "required", "document_text")])
                },
                new MatchingEvaluationJob("job-b", "job-b", "en", "Docker", "", "Operations role")
                {
                    ConfirmedRevision = new("user_confirmed", 1, true,
                        [new("C#", "optional", "user_statement")])
                }
            ],
            Labels =
            [
                new("person-a", "job-a", 3),
                new("person-b", "job-a", 0)
            ]
        };
        var path = Path.Combine(Path.GetTempPath(), $"matching-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(dataset, MatchingEvaluationRunner.JsonOptions));
            var result = await MatchingEvaluationRunner.RunAsync(path, 1, new KeywordBaselineOptions(), default);

            Assert.Equal(2, result.LabeledPairCount);
            Assert.Equal(64, result.SourceAssemblySha256.Length);
            Assert.Equal(2, result.CvToJobs.Metrics.QueryCount);
            Assert.Equal(2, result.JobsToResumes.Metrics.QueryCount);
            Assert.Equal(0.5, result.CvToJobs.Metrics.OverallLabeledPairCoverage);
            Assert.Null(result.CvToJobs.Metrics.PrecisionAtKOnCompleteQueries);
            Assert.Equal(1d, result.CvToJobs.Metrics.MeanRecallAtKAmongLabeledPositives);
            Assert.Contains(result.CvToJobs.TopResults, x => x.ResumeGroupId == "person-a" && x.JobGroupId == "job-a");
            Assert.Contains(result.CvToJobs.TopResults, x => x.ResumeGroupId == "person-b" && x.JobGroupId == "job-b");
            var revisionEvaluation = Assert.IsType<MatchingRevisionEvaluation>(result.ConfirmedRevisionEvaluation);
            Assert.Equal(2, revisionEvaluation.LabeledPairCount);
            Assert.Equal(1, revisionEvaluation.CandidateViewEligiblePairs);
            Assert.Equal(1, revisionEvaluation.CandidateViewPairsWithOverlap);
            Assert.Equal(1, revisionEvaluation.PublicViewEligiblePairs);
            Assert.Equal(0, revisionEvaluation.PublicViewPairsWithOverlap);
            Assert.Equal(1, revisionEvaluation.ExcludedCandidatePairsByStatus["resume_revision_unconfirmed"]);
            var highRelevance = Assert.Single(revisionEvaluation.ByRelevance, x => x.Relevance == 3);
            Assert.Equal(1, highRelevance.CandidateViewMatchedRequiredRequirements);
            Assert.Equal(0, highRelevance.PublicViewMatchedRequiredRequirements);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Theory]
    [InlineData("{\"datasetId\":\"x\",\"split\":\"x\",\"resumes\":null,\"jobs\":[],\"labels\":[]}")]
    [InlineData("{\"datasetId\":\"x\",\"split\":\"x\",\"resumes\":[null],\"jobs\":[],\"labels\":[]}")]
    public async Task EvaluationRejectsMalformedDatasetsWithStableDataError(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), $"matching-invalid-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, json);
            await Assert.ThrowsAsync<InvalidDataException>(() => MatchingEvaluationRunner.RunAsync(
                path, null, new KeywordBaselineOptions(), default));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
