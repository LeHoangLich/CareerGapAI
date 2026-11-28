using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Accounts;
using CareerGap.Api.Administration;
using CareerGap.Api.Community;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class CommunityTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private const string Root = "/api/community";
    private async Task<(HttpClient Client, Guid Id, string Handle)> Member(string role = "Candidate", string? password = null)
    {
        var (client, id) = await fixture.User(role, password);
        var handle = "u" + id.ToString("N")[..15];
        var response = await client.PutAsJsonAsync(Root + "/me", new ProfileEdit(handle, "Synthetic Member", "Public bio", null, true, 0));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (client, id, handle);
    }
    private static PostEdit Edit(string body = "A useful synthetic career story for the community.", bool submit = true, long version = 0, Guid? image = null) =>
        new("Synthetic career story", "Summary for testing", body, "technical", ["react", "career", ".net"], image, submit, version);
    private static async Task<SocialPost> Create(HttpClient client, PostEdit? input = null)
    {
        var response = await client.PostAsJsonAsync(Root + "/posts", input ?? Edit());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SocialPost>())!;
    }
    private static async Task State(HttpClient admin, SocialPost post, string action = "approve") =>
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{Root}/posts/{post.Id}/state", new ModerationAction(action, "Synthetic moderation reason", post.RowVersion))).StatusCode);
    private static async Task<SocialPost> Read(HttpClient client, Guid id) => (await client.GetFromJsonAsync<SocialPost>($"{Root}/posts/{id}"))!;

    [Fact] public async Task PublicVisibilityApprovalEditsOwnershipAndStaleVersions()
    {
        var a = await Member(); var b = await Member(); var (admin, _) = await fixture.User("Admin"); var guest = fixture.App.CreateClient();
        var draft = await Create(a.Client, Edit(submit: false));
        foreach (var client in new[] { guest, b.Client, admin }) Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Root}/posts/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await a.Client.PostAsJsonAsync($"{Root}/posts/{draft.Id}/state", new ModerationAction("approve", "Cannot self approve", draft.RowVersion))).StatusCode);
        await State(a.Client, draft, "submit"); var pending = await Read(a.Client, draft.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/posts/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.PutAsJsonAsync($"{Root}/posts/{draft.Id}", Edit(version: pending.RowVersion))).StatusCode);
        await State(admin, pending); var published = await Read(guest, draft.Id);
        Assert.Equal("published", published.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.PutAsJsonAsync($"{Root}/posts/{draft.Id}", Edit(version: pending.RowVersion))).StatusCode);
        var edited = await a.Client.PutAsJsonAsync($"{Root}/posts/{draft.Id}", Edit("Edited contents need another admin approval.", version: published.RowVersion));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Equal("pending", (await edited.Content.ReadFromJsonAsync<SocialPost>())!.Status);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/posts/{draft.Id}")).StatusCode);
        var feed = await guest.GetFromJsonAsync<SocialPage<SocialPost>>($"{Root}/posts?author={a.Id}"); Assert.Empty(feed!.Items);
        await FoundationFixture.Csrf(guest);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PutAsJsonAsync($"{Root}/posts/{draft.Id}/vote", new { })).StatusCode);
        var noCsrf = fixture.App.CreateClient(); Assert.Equal(HttpStatusCode.BadRequest, (await noCsrf.PostAsJsonAsync(Root + "/posts", Edit())).StatusCode);
    }

    [Fact] public async Task ProfilesAreOptInAndDoNotExposePrivateCvOrContactFields()
    {
        var (unregistered, id) = await fixture.User(); var guest = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/profiles/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await unregistered.PostAsJsonAsync(Root + "/posts", Edit())).StatusCode);
        var a = await Member(); var value = await guest.GetStringAsync($"{Root}/profiles/{a.Id}");
        Assert.DoesNotContain("email", value, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("resume", value, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.PutAsJsonAsync(Root + "/me", new ProfileEdit(a.Handle, "Other name", null, null, true, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.PutAsJsonAsync(Root + "/me", new ProfileEdit("newhandle", "Other name", null, null, true, 1))).StatusCode);
        var cv = await FoundationFixture.Upload(a.Client, FoundationFixture.Docx("Synthetic private document never shared in community."));
        Assert.Contains((await guest.GetAsync("/api/files/" + cv.GetProperty("fileId").GetGuid())).StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.PostAsJsonAsync(Root + "/posts", Edit() with { Tags = ["<script>"] })).StatusCode);
    }

    [Fact] public async Task FollowingPersonalFeedMentionsAndNotificationPrivacy()
    {
        var a = await Member(); var b = await Member(); var (admin, _) = await fixture.User("Admin");
        Assert.Equal(HttpStatusCode.BadRequest, (await a.Client.PutAsync($"{Root}/profiles/{a.Id}/follow", null)).StatusCode);
        for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.OK, (await b.Client.PutAsync($"{Root}/profiles/{a.Id}/follow", null)).StatusCode);
        var p = await Create(a.Client, Edit($"Useful public career discussion with @{b.Handle} and @{a.Handle}."));
        var before = await b.Client.GetFromJsonAsync<JsonElement>(Root + "/notifications"); Assert.DoesNotContain(before.GetProperty("items").EnumerateArray(), n => n.GetProperty("kind").GetString() == "mention");
        await State(admin, p);
        var following = await b.Client.GetFromJsonAsync<SocialPage<SocialPost>>(Root + "/posts?mode=following"); Assert.Contains(following!.Items, x => x.Id == p.Id);
        var notices = await b.Client.GetFromJsonAsync<JsonElement>(Root + "/notifications");
        Assert.Single(notices.GetProperty("items").EnumerateArray(), n => n.GetProperty("kind").GetString() == "mention");
        var notice = notices.GetProperty("items").EnumerateArray().First(); var noticeId = notice.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await a.Client.PutAsync($"{Root}/notifications/{noticeId}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.Client.PutAsync(Root + "/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await b.Client.GetFromJsonAsync<JsonElement>(Root + "/notifications")).GetProperty("unread").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await b.Client.DeleteAsync($"{Root}/profiles/{a.Id}/follow")).StatusCode);
        following = await b.Client.GetFromJsonAsync<SocialPage<SocialPost>>(Root + "/posts?mode=following"); Assert.DoesNotContain(following!.Items, x => x.Id == p.Id);
    }

    [Fact] public async Task ConcurrentVotesBookmarksAndViewsAreIdempotent()
    {
        var a = await Member(); var b = await Member(); var (admin, _) = await fixture.User("Admin"); var guest = fixture.App.CreateClient();
        var p = await Create(a.Client); await State(admin, p);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => b.Client.PutAsync($"{Root}/posts/{p.Id}/vote", null)));
        Assert.All(results, x => Assert.Equal(HttpStatusCode.OK, x.StatusCode));
        await b.Client.PutAsync($"{Root}/posts/{p.Id}/bookmark", null); await b.Client.PutAsync($"{Root}/posts/{p.Id}/bookmark", null);
        var first = await Read(guest, p.Id); var second = await Read(guest, p.Id);
        Assert.Equal(1, first.Votes); Assert.Equal(first.Views, second.Views); Assert.Equal(1, first.Bookmarks);
        Assert.Contains((await b.Client.GetFromJsonAsync<SocialPage<SocialPost>>(Root + "/posts?mode=saved"))!.Items, x => x.Id == p.Id);
        await b.Client.DeleteAsync($"{Root}/posts/{p.Id}/vote"); await b.Client.DeleteAsync($"{Root}/posts/{p.Id}/vote");
        Assert.Equal(0, (await Read(guest, p.Id)).Votes);
    }

    [Fact] public async Task RepliesAreOneLevelAndDeletedParentsKeepNoPrivateBody()
    {
        var a = await Member(); var b = await Member(); var (admin, _) = await fixture.User("Admin"); var guest = fixture.App.CreateClient();
        var p = await Create(a.Client); await State(admin, p); var other = await Create(a.Client); await State(admin, other);
        async Task<Guid> Comment(Guid post, string body, Guid? parent = null) {
            var result = await b.Client.PostAsJsonAsync($"{Root}/posts/{post}/comments", new CommentEdit(body, parent, 0)); Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            return (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }
        var parent = await Comment(p.Id, "Original comment to be erased"); var reply = await Comment(p.Id, "A one-level reply", parent);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Client.PostAsJsonAsync($"{Root}/posts/{p.Id}/comments", new CommentEdit("Nested reply not allowed", reply, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Client.PostAsJsonAsync($"{Root}/posts/{other.Id}/comments", new CommentEdit("Wrong post not allowed", parent, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.Client.DeleteAsync($"{Root}/posts/{p.Id}/comments/{parent}?rowVersion=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await b.Client.PutAsJsonAsync($"{Root}/posts/{p.Id}/comments/{parent}", new CommentEdit("stale contents", null, 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await b.Client.DeleteAsync($"{Root}/posts/{p.Id}/comments/{parent}?rowVersion=1")).StatusCode);
        var comments = await guest.GetFromJsonAsync<SocialPage<SocialComment>>($"{Root}/posts/{p.Id}/comments");
        Assert.True(comments!.Items.Single(x => x.Id == parent).IsDeleted); Assert.DoesNotContain(comments.Items, x => x.Body.Contains("Original comment"));
        Assert.Contains(comments.Items, x => x.Id == reply);
    }

    [Fact] public async Task ReportsRequireAdminAndSupportWarningsTrashAndAudit()
    {
        var a = await Member(); var b = await Member(); var (admin, _) = await fixture.User("Admin"); var guest = fixture.App.CreateClient();
        var p = await Create(a.Client); await State(admin, p);
        var result = await b.Client.PostAsJsonAsync(Root + "/reports", new ReportCreate(p.Id, null, "Synthetic report reason")); Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var id = (await result.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await b.Client.PostAsJsonAsync(Root + "/reports", new ReportCreate(p.Id, null, "duplicate report"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await b.Client.GetAsync(Root + "/admin/reports")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{Root}/admin/reports/{id}", new ModerationAction("warn", "Please respect community rules", 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/posts/{p.Id}")).StatusCode);
        Assert.Contains((await a.Client.GetFromJsonAsync<JsonElement>(Root + "/notifications")).GetProperty("items").EnumerateArray(), n => n.GetProperty("kind").GetString() == "warning");
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Root}/admin/reports/{id}", new ModerationAction("dismiss", "stale action", 0))).StatusCode);
        var hidden = await Read(a.Client, p.Id); await State(admin, hidden, "delete"); var deleted = await Read(a.Client, p.Id); await State(admin, deleted, "restore");
        Assert.Equal("hidden", (await Read(a.Client, p.Id)).Status); Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/posts/{p.Id}")).StatusCode);
        using var scope = fixture.App.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.True(await db.AdminAudits.AnyAsync(x => x.TargetId == id && x.Action == "community_report_warn"));
    }

    [Fact] public async Task ImagesAreDecodedPrivateUntilPublishedAndProtectedByOwnership()
    {
        var a = await Member(); var b = await Member(); var (admin, _) = await fixture.User("Admin"); var guest = fixture.App.CreateClient();
        async Task<HttpResponseMessage> Upload(byte[] bytes, string name) { using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(bytes), "file", name); return await a.Client.PostAsync(Root + "/images", form); }
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"), "x.png")).StatusCode);
        using var bitmap = new SkiaSharp.SKBitmap(4, 4); bitmap.Erase(SkiaSharp.SKColors.SeaGreen);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var response = await Upload(encoded.ToArray(), "synthetic.png"); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/images/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await b.Client.PostAsJsonAsync(Root + "/posts", Edit(image: id))).StatusCode);
        var p = await Create(a.Client, Edit(image: id)); Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/images/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"{Root}/images/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await a.Client.DeleteAsync($"{Root}/images/{id}")).StatusCode);
        await State(admin, p); var visible = await guest.GetAsync($"{Root}/images/{id}"); Assert.Equal("image/png", visible.Content.Headers.ContentType!.MediaType);
        await State(admin, await Read(a.Client, p.Id), "hide"); Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/images/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.Client.DeleteAsync($"{Root}/images/{id}")).StatusCode);

        // JPEG uploads are re-encoded cleanly as image/jpeg outside the DB transaction.
        using var jpgEncoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        var jpgUpload = await Upload(jpgEncoded.ToArray(), "photo.jpg");
        Assert.Equal(HttpStatusCode.Created, jpgUpload.StatusCode);
        var jpgId = (await jpgUpload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var jpgOwn = await a.Client.GetAsync($"{Root}/images/{jpgId}");
        Assert.Equal(HttpStatusCode.OK, jpgOwn.StatusCode);
        Assert.Equal("image/jpeg", jpgOwn.Content.Headers.ContentType!.MediaType);
    }

    [Fact] public async Task SuspensionAndAccountPurgeRemovePublicCommunityAndStorage()
    {
        const string password = "Community-Tests!2026";
        var a = await Member(password: password); var b = await Member(); var (admin, _) = await fixture.User("Admin"); var guest = fixture.App.CreateClient();
        Guid imageId;
        using (var scope = fixture.App.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>(); var storage = scope.ServiceProvider.GetRequiredService<CareerGap.Api.Files.IFileStorage>();
            var media = new CommunityImage { OwnerId = a.Id, ObjectKey = Guid.NewGuid().ToString("N"), Provider = storage.Provider, ContentType = "image/png" };
            await storage.PutAsync(media.ObjectKey, [1, 2, 3], media.ContentType, default); db.CommunityImages.Add(media); await db.SaveChangesAsync(); imageId = media.Id;
        }
        var p = await Create(a.Client); await State(admin, p);
        await b.Client.PutAsync($"{Root}/profiles/{a.Id}/follow", null);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/accounts/{a.Id}", new AdminAction("suspend", "Synthetic test suspension"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/posts/{p.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await a.Client.GetAsync(Root + "/notifications")).StatusCode);
        await admin.PostAsJsonAsync($"/api/admin/accounts/{a.Id}", new AdminAction("activate", "Synthetic reactivation"));
        using (var scope = fixture.App.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDb>(); var email = await db.Users.Where(x => x.Id == a.Id).Select(x => x.Email).SingleAsync(); await FoundationFixture.Csrf(a.Client); Assert.Equal(HttpStatusCode.NoContent, (await a.Client.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode); }
        await FoundationFixture.Csrf(a.Client);
        Assert.Equal(HttpStatusCode.Accepted, (await a.Client.PostAsJsonAsync("/api/account/deletion-request", new { password })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"{Root}/profiles/{a.Id}")).StatusCode);
        fixture.Clock.Advance(TimeSpan.FromDays(8));
        using (var scope = fixture.App.Services.CreateScope()) Assert.True(await scope.ServiceProvider.GetRequiredService<AccountDeletionService>().ProcessOneDueAsync("community-purge-test", default));
        using (var scope = fixture.App.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>(); Assert.False(await db.CommunityProfiles.AnyAsync(x => x.UserId == a.Id));
            var erased = await db.CommunityPosts.SingleAsync(x => x.Id == p.Id); Assert.Equal("", erased.Body); Assert.Equal("deleted", erased.Status);
            Assert.False(await db.CommunityFollows.AnyAsync(x => x.FollowingId == a.Id || x.FollowerId == a.Id));
            Assert.False(await db.CommunityImages.AnyAsync(x => x.Id == imageId));
            var request = await db.AccountDeletionRequests.SingleAsync(x => x.UserId == a.Id);
            Assert.Equal("purged", request.Status);
            Assert.All(await db.AccountDeletionObjects.Where(x => x.RequestId == request.Id).ToArrayAsync(), x => Assert.Equal("deleted", x.Status));
        }
    }

    [Fact] public async Task LiveEventsRevalidateRevokedSessionsAndLimitConcurrentConnections()
    {
        var a = await Member(); var (admin, _) = await fixture.User("Admin");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        using var first = await a.Client.SendAsync(new(HttpMethod.Get, Root + "/events"), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal("text/event-stream", first.Content.Headers.ContentType!.MediaType);
        using var reader = new StreamReader(await first.Content.ReadAsStreamAsync(timeout.Token));
        Assert.Equal("event: update", await reader.ReadLineAsync(timeout.Token));
        Assert.StartsWith("data: ", await reader.ReadLineAsync(timeout.Token));
        using var second = await a.Client.SendAsync(new(HttpMethod.Get, Root + "/events"), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var third = await a.Client.SendAsync(new(HttpMethod.Get, Root + "/events"), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/admin/accounts/{a.Id}", new AdminAction("suspend", "Revoke a live synthetic stream"))).StatusCode);
        while (await reader.ReadLineAsync(timeout.Token) is not null) { }
    }

    [Fact] public async Task MentionOptOutAndSearchPaginationDoNotRevealPendingContent()
    {
        var a = await Member(); var b = await Member(); var (admin, _) = await fixture.User("Admin"); var guest = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await b.Client.PutAsJsonAsync(Root + "/me", new ProfileEdit(b.Handle, "Quiet Member", null, null, false, 1))).StatusCode);
        var pending = await Create(a.Client, Edit("A pending private search needle confidentialneedle."));
        var p = await Create(a.Client, Edit($"A public mention @{b.Handle} without notification.")); await State(admin, p);
        Assert.DoesNotContain((await b.Client.GetFromJsonAsync<JsonElement>(Root + "/notifications")).GetProperty("items").EnumerateArray(), n => n.GetProperty("kind").GetString() == "mention");
        var search = await guest.GetFromJsonAsync<SocialPage<SocialPost>>(Root + "/posts?q=confidentialneedle"); Assert.Empty(search!.Items);
        var filtered = await guest.GetFromJsonAsync<SocialPage<SocialPost>>($"{Root}/posts?tag=react&category=technical&author={a.Id}&sort=trending"); Assert.Single(filtered!.Items, x => x.Id == p.Id);
        Assert.DoesNotContain(filtered.Items, x => x.Id == pending.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.GetAsync(Root + "/posts?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync(Root + "/posts?mode=saved")).StatusCode);
    }

    [Fact] public async Task MutationsRefreshAccountStateEvenWhenMiddlewareAlreadyTrackedTheUser()
    {
        var a = await Member(); using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal("active", (await db.Users.SingleAsync(x => x.Id == a.Id)).AccountStatus);
        await db.Users.Where(x => x.Id == a.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.AccountStatus, "suspended"));
        var error = await Assert.ThrowsAsync<CommunityError>(() => scope.ServiceProvider.GetRequiredService<CommunityService>().SavePost(a.Id, null, Edit(), default));
        Assert.Equal(401, error.Status); Assert.False(await db.CommunityPosts.AnyAsync(x => x.AuthorId == a.Id));
    }

    [Fact] public async Task ImageAccessIsCheckedAgainAfterStorageReadWhenPostIsHidden()
    {
        var a = await Member(); var (admin, _) = await fixture.User("Admin");
        Guid imageId; IFileStorage storage;
        using (var scope = fixture.App.Services.CreateScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>(); storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
            var media = new CommunityImage { OwnerId = a.Id, ObjectKey = Guid.NewGuid().ToString("N"), Provider = storage.Provider, ContentType = "image/png" };
            await storage.PutAsync(media.ObjectKey, [1, 2, 3], media.ContentType, default); db.CommunityImages.Add(media); await db.SaveChangesAsync(); imageId = media.Id;
        }
        var post = await Create(a.Client, Edit(image: imageId)); await State(admin, post);
        var gate = new ReadGate(storage);
        await using var app = fixture.App.WithWebHostBuilder(builder => builder.ConfigureServices(services => { services.RemoveAll<IFileStorage>(); services.AddSingleton<IFileStorage>(gate); }));
        using var client = app.CreateClient(); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var pending = client.GetAsync($"{Root}/images/{imageId}", timeout.Token);
        await gate.Started.Task.WaitAsync(timeout.Token);
        await State(admin, await Read(a.Client, post.Id), "hide"); gate.Release.TrySetResult();
        Assert.Equal(HttpStatusCode.NotFound, (await pending).StatusCode);
    }

    private sealed class ReadGate(IFileStorage inner) : IFileStorage
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Provider => inner.Provider;
        public Task PutAsync(string key, byte[] bytes, string type, CancellationToken ct) => inner.PutAsync(key, bytes, type, ct);
        public Task DeleteAsync(string key, CancellationToken ct) => inner.DeleteAsync(key, ct);
        public async Task<byte[]> ReadAsync(string key, CancellationToken ct) { var result = await inner.ReadAsync(key, ct); Started.TrySetResult(); await Release.Task.WaitAsync(ct); return result; }
    }
}
