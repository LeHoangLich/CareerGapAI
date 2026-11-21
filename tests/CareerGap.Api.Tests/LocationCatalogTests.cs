using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class LocationCatalogUnitTests
{
 [Fact] public void StandardizesKnownNamesAndPreservesSpecificAddresses(){
  Assert.Equal(34,LocationCatalog.Items.Length);Assert.Equal(34,LocationCatalog.Items.Select(x=>x.Id).Distinct().Count());
  Assert.Equal("Hà Nội",LocationCatalog.Normalize("Ha Noi"));Assert.Equal("Hồ Chí Minh",LocationCatalog.Normalize("TP.HCM"));
  Assert.Equal("Bình Dương",LocationCatalog.Normalize("Bình Dương"));Assert.Equal("Quận 1, TP.HCM",LocationCatalog.Normalize("Quận 1, TP.HCM"));Assert.Null(LocationCatalog.Normalize("  "));
 }
}
public sealed partial class PairMatchingTests
{
 [Fact] public async Task LocationSearchUsesJobBeforeCompanyAndMatchesAliases(){
  var s=await Prepare();using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();var job=await db.Jobs.AsNoTracking().SingleAsync(x=>x.Id==s.Job);
  var company=await db.Companies.AsNoTracking().SingleAsync(x=>x.Id==job.CompanyId);
  JobContentRequest Content(string? location)=>new("F16 unique location test","Synthetic role","React",null,null,location,"full_time","remote",null,null,null,null,null,null,"vi");
  async Task<JobDto> Create(string? location){var response=await s.Recruiter.PostAsJsonAsync("/api/jobs",new JobCreateRequest(company.Id,null,Content(location)));Assert.Equal(HttpStatusCode.Created,response.StatusCode);var dto=(await response.Content.ReadFromJsonAsync<JobDto>())!;await s.Recruiter.PostAsJsonAsync($"/api/jobs/{dto.Id}/publish",new {dto.RowVersion});return dto;}
  var dn=await Create("Da Nang");Assert.Equal("Đà Nẵng",dn.Version.Location);await Create(null);var hcm=await Create("TP.HCM");Assert.Equal("Hồ Chí Minh",hcm.Version.Location);
  var guest=fixture.App.CreateClient();async Task<int> Count(string location)=> (await guest.GetFromJsonAsync<JsonElement>("/api/jobs?q=F16%20unique%20location%20test&location="+Uri.EscapeDataString(location))).GetProperty("total").GetInt32();
  Assert.Equal(1,await Count("Hà Nội"));Assert.Equal(1,await Count("Ha Noi"));Assert.Equal(1,await Count("Da Nang"));Assert.Equal(1,await Count("HCM"));
  var catalog=await guest.GetFromJsonAsync<JsonElement>("/api/locations?q=HCM");Assert.Equal("Hồ Chí Minh",catalog.GetProperty("items")[0].GetProperty("name").GetString());
  Assert.Equal(HttpStatusCode.BadRequest,(await guest.GetAsync("/api/locations?q="+new string('a',101))).StatusCode);
 }
}
