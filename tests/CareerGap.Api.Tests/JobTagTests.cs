using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed partial class PairMatchingTests
{
 [Fact] public async Task TagsPersistPreserveLegacyEditsAndFilterBeforePagination(){
  var s=await Prepare();using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();var current=await s.Recruiter.GetFromJsonAsync<JobDto>($"/api/jobs/{s.Job}");
  var owned=await db.Jobs.AsNoTracking().SingleAsync(x=>x.Id==s.Job);var content=new JobContentRequest("F15 tag fixture","Synthetic role","React",null,null,"Hà Nội","full_time","remote",null,null,null,null,null,null,"vi");
  var create=await s.Recruiter.PostAsJsonAsync("/api/jobs",new JobCreateRequest(owned.CompanyId,null,content,["React","Docker","React"]));Assert.Equal(HttpStatusCode.Created,create.StatusCode);var job=(await create.Content.ReadFromJsonAsync<JobDto>())!;Assert.Equal(2,job.Tags!.Length);
  var (other,_)=await fixture.User("Recruiter");Assert.Equal(HttpStatusCode.NotFound,(await other.PutAsJsonAsync($"/api/jobs/{job.Id}",new JobUpdateRequest(job.RowVersion,null,content,["Java"]))).StatusCode);
  var update=await s.Recruiter.PutAsJsonAsync($"/api/jobs/{job.Id}",new JobUpdateRequest(job.RowVersion,null,content));Assert.Equal(HttpStatusCode.OK,update.StatusCode);job=(await update.Content.ReadFromJsonAsync<JobDto>())!;Assert.Contains("Docker",job.Tags!);
  var publish=await s.Recruiter.PostAsJsonAsync($"/api/jobs/{job.Id}/publish",new {job.RowVersion});Assert.Equal(HttpStatusCode.OK,publish.StatusCode);
  var guest=fixture.App.CreateClient();var all=await guest.GetFromJsonAsync<JsonElement>("/api/jobs?tags=React,Docker&tagMode=all&pageSize=1");Assert.Equal(1,all.GetProperty("total").GetInt32());Assert.Equal(job.Id,all.GetProperty("items")[0].GetProperty("id").GetGuid());
  var none=await guest.GetFromJsonAsync<JsonElement>("/api/jobs?tags=React,Java&tagMode=all");Assert.Equal(0,none.GetProperty("total").GetInt32());
  var any=await guest.GetFromJsonAsync<JsonElement>("/api/jobs?tags=React,Java&tagMode=any");Assert.Equal(1,any.GetProperty("total").GetInt32());
  Assert.Equal(HttpStatusCode.BadRequest,(await guest.GetAsync("/api/jobs?tags=unknown")).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await guest.GetAsync("/api/jobs?tagMode=bad")).StatusCode);
  var invalid=await s.Recruiter.PostAsJsonAsync("/api/jobs",new JobCreateRequest(owned.CompanyId,null,content,Enumerable.Repeat("React",11).ToArray()));Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
 }
}
