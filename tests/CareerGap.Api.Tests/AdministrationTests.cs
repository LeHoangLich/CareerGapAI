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
    [Fact] public async Task AdministrationSuspensionReportsAndModerationKeepPrivateCvPrivate()
    {
        var s = await Prepare(); var (admin, adminId) = await fixture.User("Admin"); var (other, _) = await fixture.User();
        foreach (var client in new[] { s.Candidate, s.Recruiter, other, fixture.App.CreateClient() })
            Assert.Contains((await client.GetAsync("/api/admin/accounts")).StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized });
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/resume-versions/{s.Cv}/revisions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Recruiter.PostAsJsonAsync("/api/reports", new ReportRequest("public_resume", s.Cv, "Synthetic report"))).StatusCode);
        var response = await s.Candidate.PostAsJsonAsync("/api/reports", new ReportRequest("job", s.Job, "Synthetic content report")); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var reportId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/reports/{reportId}", new AdminAction("resolved", "Reviewed synthetic report", 0))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/reports/{reportId}", new AdminAction("dismissed", "Concurrent stale decision", 0))).StatusCode);
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>(); var job = await db.Jobs.AsNoTracking().SingleAsync(x => x.Id == s.Job);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/jobs/{s.Job}", new AdminAction("archive", "Synthetic moderation", job.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Candidate.GetAsync($"/api/jobs/{s.Job}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await s.Recruiter.PostAsJsonAsync($"/api/jobs/{s.Job}/publish", new { rowVersion = job.RowVersion+1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/jobs/{s.Job}", new AdminAction("restore", "Restore as closed", job.RowVersion+1))).StatusCode);
        Assert.Equal("closed", (await db.Jobs.AsNoTracking().SingleAsync(x=>x.Id==s.Job)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/api/admin/accounts/{adminId}", new AdminAction("suspend", "Self suspend denied"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/accounts/{s.CandidateId}", new AdminAction("suspend", "Synthetic suspension"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await s.Candidate.GetAsync("/api/resumes")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/accounts/{s.CandidateId}", new AdminAction("activate", "Synthetic reinstatement"))).StatusCode);
        await FoundationFixture.Csrf(s.Candidate);
        Assert.Equal(HttpStatusCode.NoContent, (await s.Candidate.PostAsJsonAsync("/api/auth/login", new { email = (await db.Users.AsNoTracking().SingleAsync(x=>x.Id==s.CandidateId)).Email, password = CandidatePassword })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/usage")).StatusCode);
        var audit = await admin.GetStringAsync("/api/admin/audit"); Assert.Contains("account_suspend", audit); Assert.DoesNotContain("React Developer", audit);
    }
}
