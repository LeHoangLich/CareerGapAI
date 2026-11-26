using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;
public sealed partial class PairMatchingTests
{
    [Fact] public async Task AnalysisHistoryPagesAllRunsWithoutNewAiAndOpensExactOlderRun()
    {
        var s=await Prepare();var first=await Enqueue(s.Candidate,s);await Drain(first);
        var ids=new HashSet<Guid>{first};for(var i=0;i<60;i++)ids.Add(await Enqueue(s.Candidate,s));
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        var jobs=await db.AiJobs.CountAsync();var attempts=await db.ExternalCallAttempts.CountAsync();
        var seen=new HashSet<Guid>();
        for(var page=1;page<=7;page++)
        {
            var response=await s.Candidate.GetFromJsonAsync<JsonElement>($"/api/matching/history?page={page}");
            Assert.Equal(61,response.GetProperty("total").GetInt32());
            Assert.Equal(page==7?1:10,response.GetProperty("items").GetArrayLength());
            foreach(var item in response.GetProperty("items").EnumerateArray())
            {
                Assert.True(seen.Add(item.GetProperty("id").GetGuid()));Assert.True(item.GetProperty("available").GetBoolean());
                Assert.Equal(50m,item.GetProperty("score").GetDecimal());Assert.Equal("React Developer",item.GetProperty("jobTitle").GetString());
            }
        }
        Assert.True(ids.SetEquals(seen));
        var old=await s.Candidate.GetFromJsonAsync<JsonElement>($"/api/matching/pairs/{first}");Assert.Equal(first,old.GetProperty("id").GetGuid());
        foreach(var query in new[]{"status=failed","context=application"})Assert.Equal(0,(await s.Candidate.GetFromJsonAsync<JsonElement>("/api/matching/history?"+query)).GetProperty("total").GetInt32());
        foreach(var query in new[]{"status=bad","context=bad","page=0","pageSize=51"})Assert.Equal(HttpStatusCode.BadRequest,(await s.Candidate.GetAsync("/api/matching/history?"+query)).StatusCode);
        var (other,_)=await fixture.User();Assert.Equal(0,(await other.GetFromJsonAsync<JsonElement>("/api/matching/history")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/matching/pairs/{first}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.App.CreateClient().GetAsync("/api/matching/history")).StatusCode);
        Assert.Equal(jobs,await db.AiJobs.CountAsync());Assert.Equal(attempts,await db.ExternalCallAttempts.CountAsync());
    }

    [Fact] public async Task AnalysisHistoryRedactsRevokedSourcesButKeepsOwnRunReceipt()
    {
        var s=await Prepare();var id=await Enqueue(s.Candidate,s);await Drain(id);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        var job=await db.Jobs.SingleAsync(x=>x.Id==s.Job);job.Status="archived";job.ClosedAt=DateTimeOffset.UtcNow;job.RowVersion++;await db.SaveChangesAsync();
        var response=await s.Candidate.GetFromJsonAsync<JsonElement>("/api/matching/history");var item=response.GetProperty("items")[0];
        Assert.Equal(id,item.GetProperty("id").GetGuid());Assert.False(item.GetProperty("available").GetBoolean());
        foreach(var field in new[]{"score","jobTitle","resumeVersionNo"})Assert.Equal(JsonValueKind.Null,item.GetProperty(field).ValueKind);
        Assert.Equal(HttpStatusCode.NotFound,(await s.Candidate.GetAsync($"/api/matching/pairs/{id}")).StatusCode);
        var (admin,_)=await fixture.User("Admin");Assert.Equal(HttpStatusCode.Forbidden,(await admin.GetAsync("/api/matching/history")).StatusCode);
    }
}
