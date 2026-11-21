using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Learning;
using CareerGap.Api.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class WeeklyRoadmapPlanTests
{
    private static ScoredCriterion C(string key,string status,string importance="required")=>new(key,key,importance,status,1,0,[],"",false,"document_text");
    [Fact] public void CoversEveryGapPrioritizesRequiredAndRepeatsPracticeWithoutIncludingMet()
    {
        var criteria=new[]{C("optional","no_evidence","optional"),C("required","partial"),C("met","met"),C("manual","not_assessed")};
        var plan=WeeklyRoadmap.Build(Guid.NewGuid(),criteria,4,5);
        Assert.Equal(4,plan.Schedule.Count);Assert.Equal("required",plan.Schedule[0].Focus[0].RequirementKey);
        Assert.Equal(20,plan.Schedule.Sum(x=>x.Hours));Assert.DoesNotContain(plan.Schedule.SelectMany(x=>x.Focus),x=>x.RequirementKey is "met" or "manual");
        Assert.NotEqual(plan.Schedule[0].Focus[0].Practice,plan.Schedule[2].Focus[0].Practice);
        var many=WeeklyRoadmap.Build(Guid.NewGuid(),Enumerable.Range(0,9).Select(i=>C(i.ToString(),"no_evidence")).ToArray(),2,1);
        Assert.Equal(9,many.Schedule.SelectMany(x=>x.Focus).Select(x=>x.RequirementKey).Distinct().Count());
    }
    [Fact] public void EmptyAndInvalidPlansDoNotInventGoals()
    {
        Assert.Empty(WeeklyRoadmap.Build(Guid.NewGuid(),[C("met","met")],4,5).Schedule);
        Assert.Throws<ArgumentOutOfRangeException>(()=>WeeklyRoadmap.Build(Guid.NewGuid(),[],1,5));
        Assert.Throws<ArgumentOutOfRangeException>(()=>WeeklyRoadmap.Build(Guid.NewGuid(),[],4,21));
    }
}
public sealed partial class PairMatchingTests
{
    [Fact] public async Task WeeklyRoadmapReadsOnlyOwnedSuccessfulSelfResultAndPreservesAiState()
    {
        var s=await Prepare();var id=await Enqueue(s.Candidate,s);await Drain(id);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        var jobs=await db.AiJobs.CountAsync();var attempts=await db.ExternalCallAttempts.CountAsync();
        var plan=await s.Candidate.GetFromJsonAsync<JsonElement>($"/api/learning/roadmap/{id}?weeks=4&hoursPerWeek=5");Assert.Equal(4,plan.GetProperty("schedule").GetArrayLength());
        var (other,_)=await fixture.User();Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/learning/roadmap/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await s.Recruiter.GetAsync($"/api/learning/roadmap/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.App.CreateClient().GetAsync($"/api/learning/roadmap/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await s.Candidate.GetAsync($"/api/learning/roadmap/{id}?weeks=99")).StatusCode);
        Assert.Equal(jobs,await db.AiJobs.CountAsync());Assert.Equal(attempts,await db.ExternalCallAttempts.CountAsync());
        var job=await db.Jobs.SingleAsync(x=>x.Id==s.Job);job.Status="closed";job.ClosedAt=DateTimeOffset.UtcNow;job.RowVersion++;await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound,(await s.Candidate.GetAsync($"/api/learning/roadmap/{id}")).StatusCode);
    }
}
