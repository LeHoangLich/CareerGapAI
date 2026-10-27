using CareerGap.Api.Learning;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class LearningTopicTests
{
    [Theory]
    [InlineData("Giao tiếp tốt", "marketing", false)]
    [InlineData("Giao tiếp tốt", "communication", true)]
    [InlineData("giao tiep tot", "communication", true)]
    [InlineData("Kỹ năng tiếp thị", "marketing", true)]
    [InlineData("ke toan", "accounting", true)]
    [InlineData("Quản lý dữ liệu", "management", false)]
    [InlineData("Quản lý dự án", "management", true)]
    [InlineData("Có tiếng Anh", "english", true)]
    [InlineData("JavaScript", "java", false)]
    [InlineData("Java", "java", true)]
    [InlineData("PostgreSQL", "sql", false)]
    [InlineData("PostgreSQL", "postgresql", true)]
    [InlineData("C# developer", "dotnet", true)]
    [InlineData("ASP.NET", "dotnet", true)]
    [InlineData("Kiểm   thử phần mềm", "testing", true)]
    [InlineData("Chăm sóc sức khỏe", "customer-service", false)]
    public void WholeAliasPhrasesAvoidCrossTopicAndCrossTechnologyMatches(string requirement, string topic, bool expected) =>
        Assert.Equal(expected, LearningService.MatchesTopic(requirement, LearningService.Topics.Single(x => x.Key == topic)));
}
