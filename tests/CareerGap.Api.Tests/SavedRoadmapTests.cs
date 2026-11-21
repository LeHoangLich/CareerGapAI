using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Learning;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed partial class PairMatchingTests
{
    [Fact] public async Task SavedRoadmapPersistsProgressRejectsStaleWritesAndCascadesWithSource()
    {
        var s=await Prepare();var id=await Enqueue(s.Candidate,s);await Drain(id);var url=$"/api/learning/saved-roadmaps/{id}";
        Assert.Equal(JsonValueKind.Null,(await s.Candidate.GetFromJsonAsync<JsonElement>(url)).GetProperty("saved").ValueKind);
        var create=await s.Candidate.PutAsJsonAsync(url,new SaveRoadmapRequest(4,5,0));Assert.Equal(HttpStatusCode.OK,create.StatusCode);
        var saved=await create.Content.ReadFromJsonAsync<JsonElement>();var version=saved.GetProperty("rowVersion").GetInt64();
        var key=saved.GetProperty("plan").GetProperty("schedule")[0].GetProperty("focus")[0].GetProperty("requirementKey").GetString()!;
        var update=await s.Candidate.PutAsJsonAsync(url+"/progress",new RoadmapProgressRequest(1,key,true,version));Assert.Equal(HttpStatusCode.OK,update.StatusCode);
        var v2=(await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetInt64();
        Assert.Equal(HttpStatusCode.Conflict,(await s.Candidate.PutAsJsonAsync(url,new SaveRoadmapRequest(2,10,version))).StatusCode);
        var same=await s.Candidate.PutAsJsonAsync(url,new SaveRoadmapRequest(4,5,v2));Assert.Single((await same.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("completed").EnumerateArray());
        Assert.Single((await s.Candidate.GetFromJsonAsync<JsonElement>(url)).GetProperty("saved").GetProperty("completed").EnumerateArray());
        var (other,_)=await fixture.User();Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await other.PutAsJsonAsync(url,new SaveRoadmapRequest(4,5,0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await s.Candidate.PutAsJsonAsync(url+"/progress",new RoadmapProgressRequest(99,key,true,v2))).StatusCode);
        var reset=await s.Candidate.PutAsJsonAsync(url,new SaveRoadmapRequest(2,10,v2));Assert.Empty((await reset.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("completed").EnumerateArray());
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        await using(var tx=await db.Database.BeginTransactionAsync()){await CareerGap.Api.Matching.MatchRetention.Erase(db,db.MatchInputs.Where(x=>x.AiJobId==id),default);await tx.CommitAsync();}
        Assert.False(await db.SavedRoadmaps.AnyAsync(x=>x.PairId==id));Assert.Equal(HttpStatusCode.NotFound,(await s.Candidate.GetAsync(url)).StatusCode);
    }
}
