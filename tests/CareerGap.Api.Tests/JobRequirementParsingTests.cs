using System.Text.Json;
using CareerGap.Api.Ai;
using CareerGap.Api.Recruitment;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class JobRequirementParsingTests
{
    private static JobStructuredRequirements Extract(string text)
    {
        Assert.True(JobRevisionFactory.TryExtract(Guid.NewGuid(), null, 1, text, DateTimeOffset.UtcNow, out var revision, out _));
        var evidence = JsonSerializer.Deserialize<JobRequirementEvidence[]>(revision!.Evidence, OutputValidator.Json)!;
        Assert.All(evidence, e => Assert.Equal(e.Quote, text[e.StartOffset!.Value..e.EndOffset!.Value]));
        return JsonSerializer.Deserialize<JobStructuredRequirements>(revision.StructuredData, OutputValidator.Json)!;
    }

    [Fact]
    public void ExplicitHeadingsApplyToItemsAndDoNotBecomeRequirements()
    {
        var data = Extract("## Required skills\r\n- React\r\n- TypeScript\r\n## Nice-to-have qualifications\r\n- Docker\r\n## Work environment\r\nHybrid work");
        Assert.Equal(4, data.Requirements.Count);
        Assert.Equal(new[] { "required", "required", "optional", "unspecified" }, data.Requirements.Select(x => x.Importance));
        Assert.Equal("React", data.Requirements[0].Text);
    }

    [Theory]
    [InlineData("Kinh nghiệm từ 2 năm - Không yêu cầu kinh nghiệm chuyên môn", null)]
    [InlineData("At least 3 years experience; no experience required", 3)]
    [InlineData("Minimum 4 years overall, including 2 years in testing", 4)]
    public void ExperienceNegationDoesNotPublishContradictoryYears(string text, int? firstMinimum)
    {
        var first = Extract(text).Requirements[0];
        Assert.Equal(firstMinimum is null ? (decimal?)null : firstMinimum.Value, first.MinimumYears);
        if (firstMinimum is null) Assert.True(first.NeedsReview);
    }

    [Fact]
    public void MultipleYearScopesInOneAlternativeRemainUnresolved()
    {
        var item = Assert.Single(Extract("Minimum 5 years overall or 2 years with this certification").Requirements);
        Assert.Null(item.MinimumYears); Assert.True(item.NeedsReview); Assert.Equal("any_of", item.Relation);
    }

    [Fact]
    public void AlternativesAndExamplesDoNotBecomeSeparateMandatoryTechnologies()
    {
        var data = Extract("Required: React and/or Vue; Cloud knowledge (AWS, GCP, Azure)");
        Assert.Equal(2, data.Requirements.Count);
        Assert.Equal("any_of", data.Requirements[0].Relation);
        Assert.Equal(2, data.Requirements[0].Alternatives!.Count);
        Assert.True(data.Requirements[1].NeedsReview);
        Assert.All(data.Requirements, x => Assert.Equal("required", x.Importance));
    }

    [Fact]
    public void ComparativeYearThresholdIsNotAnAlternativeQualification()
    {
        var data = Extract("Kinh nghiệm: 1 năm hoặc cao hơn\nMinimum 2 years or more");
        Assert.All(data.Requirements, x => Assert.Equal("single", x.Relation));
        Assert.Equal(new decimal?[] { 1m, 2m }, data.Requirements.Select(x => x.MinimumYears));
    }

    [Fact]
    public void LocalOptionalPhraseOverridesHeadingForOnlyItsOwnItem()
    {
        var data = Extract("## Required skills\nJava; kinh nghiệm kiểm thử là một lợi thế\n## Optional\nDocker");
        Assert.Equal(new[] { "required", "optional", "optional" }, data.Requirements.Select(x => x.Importance));
    }

    [Fact]
    public void UnresolvedConjunctionRequiresReviewInsteadOfPretendingToBeAtomic()
    {
        var item = Assert.Single(Extract("Required: REST API understanding and architecture knowledge").Requirements);
        Assert.True(item.NeedsReview); Assert.Equal("single", item.Relation);
        Assert.Null(item.Alternatives);
    }

    [Fact]
    public void DecimalYearsAndDottedTechnologiesKeepTheirSource()
    {
        var data = Extract("1. Minimum 1.5 years with Nest.js and .NET. Strong testing skills.");
        Assert.Equal(2, data.Requirements.Count); Assert.Equal(1.5m, data.Requirements[0].MinimumYears);
        Assert.Contains("Nest.js and .NET.", data.Requirements[0].Text);
    }

    [Fact]
    public void ConfirmationRetainsOriginalParserSnapshotForLegacyRevision()
    {
        Assert.True(JobRevisionFactory.TryExtract(Guid.NewGuid(), null, 1, "Required: Testing", DateTimeOffset.UtcNow, out var parent, out _));
        parent!.ParserVersion = "jd-atomic-requirements-v2";
        var item = Assert.Single(JsonSerializer.Deserialize<JobStructuredRequirements>(parent.StructuredData, OutputValidator.Json)!.Requirements);
        Assert.True(JobRevisionFactory.TryConfirm(parent, Guid.NewGuid(), 2,
            new(1, [new(item.RequirementId, item.Importance)], []), DateTimeOffset.UtcNow, out var child, out _));
        Assert.Equal("jd-atomic-requirements-v2", child!.ParserVersion);
        Assert.Equal(parent.Id, child.ParentRevisionId);
    }
}
