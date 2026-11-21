using CareerGap.Api.Recruitment;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class SkillCatalogTests
{
 [Fact] public void FindsAliasesAndUnaccentedVietnameseWithoutMergingDistinctSkills(){
  Assert.Contains(SkillCatalog.Search("ReactJS",null),s=>s.Id=="react");
  Assert.Contains(SkillCatalog.Search("giao tiep",null),s=>s.Id=="communication");
  Assert.DoesNotContain(SkillCatalog.Search("JavaScript",null),s=>s.Id=="java");
  Assert.DoesNotContain(SkillCatalog.Search("C++",null),s=>s.Id=="csharp");
  Assert.All(SkillCatalog.Search(null,"Kỹ năng chung"),s=>Assert.Equal("Kỹ năng chung",s.Category));
  Assert.Equal(SkillCatalog.Items.Length,SkillCatalog.Items.Select(s=>s.Id).Distinct().Count());
 }
}
