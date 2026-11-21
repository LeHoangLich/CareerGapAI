using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Administration;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;
public sealed partial class PairMatchingTests
{
    [Fact] public async Task ReportWorkflowReviewWarnIsAtomicAuditedAndPrivate()
    {
        var s = await Prepare(); var (admin, _) = await fixture.User("Admin"); var (admin2, _) = await fixture.User("Admin");
        var applicationResponse = await s.Candidate.PostAsJsonAsync($"/api/jobs/{s.Job}/applications", new { resumeVersionId = s.Cv, submissionKey = Guid.NewGuid(), coverLetter = "F26 preserved letter" });
        Assert.Equal(HttpStatusCode.Created, applicationResponse.StatusCode);
        var applicationId = (await applicationResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var created = await s.Candidate.PostAsJsonAsync("/api/reports", new ReportRequest("job", s.Job, "Synthetic incorrect content"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await s.Candidate.PostAsJsonAsync("/api/reports", new ReportRequest("job", s.Job, "Duplicate pending report"))).StatusCode);
        foreach (var client in new[] {s.Candidate,s.Recruiter,fixture.App.CreateClient()})
        {
            Assert.Contains((await client.GetAsync("/api/admin/reports/inbox")).StatusCode, new[]{HttpStatusCode.Forbidden,HttpStatusCode.Unauthorized});
            Assert.Contains((await client.GetAsync($"/api/admin/reports/{id}")).StatusCode, new[]{HttpStatusCode.Forbidden,HttpStatusCode.Unauthorized});
        }
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/reports/{id}");
        var version = detail.GetProperty("job").GetProperty("rowVersion").GetInt64();
        Assert.Equal("React Developer", detail.GetProperty("job").GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/resume-versions/{s.Cv}/revisions")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/reports/{id}", new AdminAction("review", "Investigating evidence", 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/reports/{id}", new AdminAction("warn", "Stale report rejected", 0, version))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/reports/{id}", new AdminAction("warn", "Stale job rejected", 1, version-1))).StatusCode);
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal("published", (await db.Jobs.AsNoTracking().SingleAsync(x=>x.Id==s.Job)).Status);
        Assert.False(await db.Notifications.AnyAsync(x=>x.DeduplicationKey=="report-warning:"+id));
        var race = await Task.WhenAll(admin.PostAsJsonAsync($"/api/admin/reports/{id}", new AdminAction("warn", "Remove synthetic violation", 1, version)), admin2.PostAsJsonAsync($"/api/admin/reports/{id}", new AdminAction("warn", "Second moderator decision", 1, version)));
        Assert.Single(race, x=>x.StatusCode==HttpStatusCode.NoContent); Assert.Single(race,x=>x.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal("archived", (await db.Jobs.AsNoTracking().SingleAsync(x=>x.Id==s.Job)).Status);
        Assert.Equal(1, await db.Notifications.CountAsync(x=>x.DeduplicationKey=="report-warning:"+id));
        Assert.Equal(2, await db.AdminAudits.CountAsync(x=>x.TargetId==id));
        Assert.Contains("F26 preserved letter", await s.Recruiter.GetStringAsync($"/api/applications/{applicationId}"));
        Assert.Contains("F26 preserved letter", await s.Candidate.GetStringAsync($"/api/applications/{applicationId}"));
        Assert.Equal(HttpStatusCode.NotFound, (await s.Candidate.GetAsync($"/api/jobs/{s.Job}")).StatusCode);
        Assert.Contains("Cảnh cáo về tin tuyển dụng", await s.Recruiter.GetStringAsync("/api/notifications/inbox?type=system_notice"));
        detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/reports/{id}");
        Assert.Equal("resolved", detail.GetProperty("report").GetProperty("status").GetString());
        Assert.Equal(2, detail.GetProperty("history").GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/reports/{id}", new AdminAction("dismissed", "Cannot reopen report", 2))).StatusCode);
    }

    [Fact] public async Task ReportWorkflowFiltersPagesAndMissingTargetsKeepHistory()
    {
        var s=await Prepare();var (admin,_)=await fixture.User("Admin");
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        var time=DateTimeOffset.UtcNow; var rows=Enumerable.Range(0,61).Select(i=>new ContentReport{ReporterId=s.CandidateId,TargetId=s.Job,Reason="Synthetic pagination "+i,CreatedAt=time,Status=i%2==0?"open":"reviewed"}).ToArray();
        db.ContentReports.AddRange(rows);await db.SaveChangesAsync();
        var ids=new HashSet<Guid>();var total=0;
        for(var page=1;page<=100;page++)
        {
            var result=await admin.GetFromJsonAsync<JsonElement>($"/api/admin/reports/inbox?status=all&targetType=job&page={page}");total=result.GetProperty("total").GetInt32();
            foreach(var row in result.GetProperty("items").EnumerateArray())Assert.True(ids.Add(row.GetProperty("id").GetGuid()));
            if(page*10>=total)break;
        }
        Assert.Equal(total,ids.Count);Assert.All(rows,x=>Assert.Contains(x.Id,ids));
        var filtered=await admin.GetFromJsonAsync<JsonElement>("/api/admin/reports/inbox?status=reviewed&targetType=job");
        Assert.All(filtered.GetProperty("items").EnumerateArray(),x=>Assert.Equal("reviewed",x.GetProperty("status").GetString()));
        foreach(var query in new[]{"page=0","page=10001","status=invalid","targetType=bad"})Assert.Equal(HttpStatusCode.BadRequest,(await admin.GetAsync("/api/admin/reports/inbox?"+query)).StatusCode);
        var missing=new ContentReport{ReporterId=s.CandidateId,TargetId=Guid.NewGuid(),Reason="Deleted target"};db.ContentReports.Add(missing);await db.SaveChangesAsync();
        var detail=await admin.GetFromJsonAsync<JsonElement>($"/api/admin/reports/{missing.Id}");Assert.Equal(JsonValueKind.Null,detail.GetProperty("job").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/admin/reports/{missing.Id}",new AdminAction("hide","Target missing",0,0))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PostAsJsonAsync($"/api/admin/reports/{missing.Id}",new AdminAction("dismissed","No target left to moderate",0))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.GetAsync("/api/admin/reports")).StatusCode);
    }

    [Fact] public async Task ReportWorkflowSuspensionProtectsAdminsAndDoesNotResolveAutomatically()
    {
        var s=await Prepare();var (admin,adminId)=await fixture.User("Admin");
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        var report=new ContentReport{ReporterId=s.CandidateId,TargetId=s.Job,Reason="Synthetic owner violation"};db.ContentReports.Add(report);await db.SaveChangesAsync();
        var job=await db.Jobs.AsNoTracking().SingleAsync(x=>x.Id==s.Job);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync($"/api/admin/reports/{report.Id}",new AdminAction("suspend"," ",0,job.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await s.Candidate.PostAsJsonAsync($"/api/admin/reports/{report.Id}",new AdminAction("suspend","Not admin",0,job.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PostAsJsonAsync($"/api/admin/reports/{report.Id}",new AdminAction("suspend","Suspend synthetic owner",0,job.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await s.Recruiter.GetAsync("/api/jobs/mine")).StatusCode);
        Assert.Equal("reviewed",(await db.ContentReports.AsNoTracking().SingleAsync(x=>x.Id==report.Id)).Status);
        Assert.Equal("published",(await db.Jobs.AsNoTracking().SingleAsync(x=>x.Id==s.Job)).Status);
        Assert.Equal(HttpStatusCode.NotFound,(await s.Candidate.GetAsync($"/api/jobs/{s.Job}")).StatusCode);
        db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>{UserId=s.RecruiterId,RoleId=await db.Roles.Where(x=>x.Name=="Admin").Select(x=>x.Id).SingleAsync()});
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/admin/reports/{report.Id}",new AdminAction("suspend","Admin protected",1,job.RowVersion))).StatusCode);
        Assert.Equal("active",(await db.Users.AsNoTracking().SingleAsync(x=>x.Id==adminId)).AccountStatus);
    }
}
