using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class RecruitmentNotificationTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task InboxPagesAllSixtyOneNotificationsWithStableTiesAndCombinesReadAndTypeFilters()
    {
        var (client, uid) = await fixture.User();var (_, otherId) = await fixture.User();
        var tied = DateTimeOffset.UtcNow.AddHours(-1);
        var notices = Enumerable.Range(0, 61).Select(i => new NotificationRecord {
            RecipientUserId = uid, NotificationType = new[] { "application_submitted", "application_status_changed", "system_notice" }[i % 3],
            Title = "F23 " + i, Body = "Synthetic notice", DeduplicationKey = "f23:" + Guid.NewGuid(),
            CreatedAt = tied, ReadAt = i % 4 == 0 ? DateTimeOffset.UtcNow.AddMinutes(-1) : null
        }).ToArray();
        var foreign = new NotificationRecord { RecipientUserId = otherId, Title = "Foreign notice", DeduplicationKey = Guid.NewGuid().ToString() };
        using var scope = fixture.App.Services.CreateScope();var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.Notifications.AddRange(notices);db.Notifications.Add(foreign);await db.SaveChangesAsync();
        var seen = new List<Guid>();
        for (var page = 1; page <= 7; page++)
        {
            var result = await client.GetFromJsonAsync<JsonElement>($"/api/notifications/inbox?page={page}&pageSize=10");
            Assert.Equal(61, result.GetProperty("total").GetInt32());Assert.Equal(45, result.GetProperty("unread").GetInt32());
            var items = result.GetProperty("items").EnumerateArray().ToArray();Assert.Equal(page == 7 ? 1 : 10, items.Length);
            seen.AddRange(items.Select(x => x.GetProperty("id").GetGuid()));
        }
        Assert.Equal(notices.Select(n => n.Id).OrderByDescending(id => id.ToString("N"), StringComparer.Ordinal), seen);
        foreach (var read in new[] { "all", "read", "unread" })
        foreach (var type in new[] { "", "application_submitted", "application_status_changed", "system_notice" })
        {
            var expected = notices.Where(n => (read == "all" || (read == "read" ? n.ReadAt != null : n.ReadAt == null)) && (type == "" || n.NotificationType == type)).ToArray();
            var result = await client.GetFromJsonAsync<JsonElement>($"/api/notifications/inbox?read={read}&pageSize=50{(type == "" ? "" : "&type=" + type)}");
            Assert.Equal(expected.Length, result.GetProperty("total").GetInt32());Assert.Equal(45, result.GetProperty("unread").GetInt32());
            Assert.All(result.GetProperty("items").EnumerateArray(), item => Assert.Contains(expected, n => n.Id == item.GetProperty("id").GetGuid()));
        }
        var legacy = await client.GetFromJsonAsync<JsonElement[]>("/api/notifications");Assert.Equal(20, legacy!.Length);
        foreach (var query in new[] { "read=bad", "type=unknown", "page=0", "pageSize=51" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/notifications/inbox?" + query)).StatusCode);
        var outside = await client.GetFromJsonAsync<JsonElement>("/api/notifications/inbox?page=999&pageSize=10");Assert.Empty(outside.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsync($"/api/notifications/{foreign.Id}/read", null)).StatusCode);
        var firstUnread = notices.First(n => n.ReadAt == null);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/notifications/{firstUnread.Id}/read", null)).StatusCode);
        var firstReadAt = (await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == firstUnread.Id)).ReadAt;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync($"/api/notifications/{firstUnread.Id}/read", null)).StatusCode);
        Assert.Equal(firstReadAt, (await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == firstUnread.Id)).ReadAt);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync("/api/notifications/read-all", null)).StatusCode);
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/notifications/inbox?read=unread");
        Assert.Equal(0, empty.GetProperty("total").GetInt32());Assert.Equal(0, empty.GetProperty("unread").GetInt32());
        Assert.Equal(61, await db.Notifications.CountAsync(n => n.RecipientUserId == uid && n.ReadAt != null));
        Assert.Null((await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == foreign.Id)).ReadAt);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsync("/api/notifications/read-all", null)).StatusCode);
    }

    [Fact]
    public async Task NotificationLinksRespectCurrentApplicationOwnershipAccountStatusAndRetention()
    {
        var (candidate, cid) = await fixture.User();var (owner, uid) = await fixture.User("Recruiter");
        var (otherRecruiter, otherId) = await fixture.User("Recruiter");var (otherCandidate, otherCandidateId) = await fixture.User();
        var version = (await FoundationFixture.Upload(candidate)).GetProperty("versionId").GetGuid();
        var company = await owner.PostAsJsonAsync("/api/companies", new { name = "F23 link test" });
        var companyId = (await company.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var created = await owner.PostAsJsonAsync("/api/jobs", new { companyId, content = new { title = "F23 target", description = "Test only", requirementsText = "Testing", employmentType = "full_time", workMode = "remote", language = "vi" } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var job = await created.Content.ReadFromJsonAsync<JsonElement>();var jid = job.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await fixture.PublishWithVerifiedBusinessFixture(owner, jid, new { rowVersion = job.GetProperty("rowVersion").GetInt64() })).StatusCode);
        var submitted = await candidate.PostAsJsonAsync($"/api/jobs/{jid}/applications", new { resumeVersionId = version, submissionKey = Guid.NewGuid(), coverLetter = "F23 private letter" });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);var id = (await submitted.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        using var scope = fixture.App.Services.CreateScope();var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        foreach (var recipient in new[] { otherId, otherCandidateId })
            db.Notifications.Add(new NotificationRecord { RecipientUserId = recipient, ApplicationId = id, Title = "Wrong reference", DeduplicationKey = Guid.NewGuid().ToString() });
        await db.SaveChangesAsync();
        foreach (var participant in new[] { candidate, owner })
        {
            var result = await participant.GetFromJsonAsync<JsonElement>("/api/notifications/inbox");
            var notice = Assert.Single(result.GetProperty("items").EnumerateArray());
            Assert.Equal($"#application?id={id}", notice.GetProperty("href").GetString());
            Assert.DoesNotContain("F23 private letter", result.ToString(), StringComparison.Ordinal);
            Assert.Equal(HttpStatusCode.OK, (await participant.GetAsync($"/api/applications/{id}")).StatusCode);
        }
        foreach (var outsider in new[] { otherCandidate, otherRecruiter })
        {
            Assert.Equal(0, (await outsider.GetFromJsonAsync<JsonElement>("/api/notifications/inbox")).GetProperty("total").GetInt32());
            Assert.Empty((await outsider.GetFromJsonAsync<JsonElement[]>("/api/notifications"))!);
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/applications/{id}")).StatusCode);
        }
        var ownerNotice = await db.Notifications.AsNoTracking().SingleAsync(n => n.ApplicationId == id && n.RecipientUserId == uid);
        await db.Users.Where(u => u.Id == cid).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "suspended"));
        Assert.Equal(0, (await owner.GetFromJsonAsync<JsonElement>("/api/notifications/inbox")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsync($"/api/notifications/{ownerNotice.Id}/read", null)).StatusCode);
        await owner.PutAsync("/api/notifications/read-all", null);
        Assert.Null((await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == ownerNotice.Id)).ReadAt);
        await db.Users.Where(u => u.Id == cid).ExecuteUpdateAsync(s => s.SetProperty(u => u.AccountStatus, "active"));
        Assert.Equal(HttpStatusCode.OK, (await candidate.PostAsJsonAsync($"/api/applications/{id}/withdraw", new { rowVersion = 0 })).StatusCode);
        await db.Applications.Where(a => a.Id == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.StatusChangedAt, DateTimeOffset.UtcNow.AddDays(-91)));
        Assert.Equal(0, (await owner.GetFromJsonAsync<JsonElement>("/api/notifications/inbox")).GetProperty("total").GetInt32());
        Assert.Equal(0, (await candidate.GetFromJsonAsync<JsonElement>("/api/notifications/inbox")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/applications/{id}")).StatusCode);
        await owner.PutAsync("/api/notifications/read-all", null);
        Assert.Null((await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == ownerNotice.Id)).ReadAt);
    }

    [Fact]
    public async Task AnonymousUsersCannotReadOrMarkRecruitmentNotifications()
    {
        var guest = fixture.App.CreateClient();
        foreach (var path in new[] { "/api/notifications", "/api/notifications/inbox" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(path)).StatusCode);
        await FoundationFixture.Csrf(guest);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PutAsync("/api/notifications/read-all", null)).StatusCode);
    }
}
