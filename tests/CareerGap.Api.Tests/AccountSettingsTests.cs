using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Accounts;
using CareerGap.Api.Community;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class AccountSettingsTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    private static byte[] TinyPng()
    {
        using var bmp = new SkiaSharp.SKBitmap(8, 8);
        bmp.Erase(SkiaSharp.SKColors.SeaGreen);
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var png = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    [Fact]
    public async Task AccountProfileSyncsIdentityAvatarAndProtectsImageFromDeletion()
    {
        var (user, uid) = await fixture.User("Candidate");
        var (other, _) = await fixture.User("Candidate");

        // Create community profile so we can verify sync to both CandidateProfile and CommunityProfile
        var commRes = await user.PutAsJsonAsync("/api/community/me", new ProfileEdit("sync_user_" + uid.ToString("N")[..6], "Old Name", "Bio", null, true, 0));
        Assert.Equal(HttpStatusCode.OK, commRes.StatusCode);

        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(TinyPng()), "file", "avatar.png");
        var upload = await user.PostAsync("/api/community/images", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var imageId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Foreign user cannot attach someone else's image
        var foreignUpdate = await other.PutAsJsonAsync("/api/account/profile", new AccountProfileUpdateRequest("Other Name", imageId, true, true));
        Assert.Equal(HttpStatusCode.BadRequest, foreignUpdate.StatusCode);

        // Owner updates display name, avatar, and notification preferences
        var update = await user.PutAsJsonAsync("/api/account/profile", new AccountProfileUpdateRequest("Lê Hoàng Lịch", imageId, false, false));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Lê Hoàng Lịch", updated.GetProperty("displayName").GetString());
        Assert.Equal(imageId, updated.GetProperty("avatarImageId").GetGuid());
        Assert.Equal($"/api/community/images/{imageId}", updated.GetProperty("avatarUrl").GetString());
        Assert.False(updated.GetProperty("notifyRecruitment").GetBoolean());
        Assert.False(updated.GetProperty("notifyCommunity").GetBoolean());

        // /api/auth/me reflects updated fields
        var me = await user.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.True(me.GetProperty("authenticated").GetBoolean());
        Assert.Equal("Lê Hoàng Lịch", me.GetProperty("displayName").GetString());
        Assert.Equal($"/api/community/images/{imageId}", me.GetProperty("avatarUrl").GetString());
        Assert.False(me.GetProperty("notifyRecruitment").GetBoolean());
        Assert.False(me.GetProperty("notifyCommunity").GetBoolean());

        // CandidateProfile and CommunityProfile are synced
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var candidateProfile = await db.CandidateProfiles.AsNoTracking().SingleAsync(x => x.UserId == uid);
            var communityProfile = await db.CommunityProfiles.AsNoTracking().SingleAsync(x => x.UserId == uid);
            Assert.Equal("Lê Hoàng Lịch", candidateProfile.DisplayName);
            Assert.Equal("Lê Hoàng Lịch", communityProfile.DisplayName);
            Assert.Equal(imageId, communityProfile.AvatarId);
        }

        // Image is readable while assigned and protected from deletion
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/community/images/{imageId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await user.DeleteAsync($"/api/community/images/{imageId}")).StatusCode);
    }

    [Fact]
    public async Task NotificationPreferencesSuppressRecruitmentAndCommunityNoticesWhenDisabled()
    {
        var (candidate, candidateId) = await fixture.User("Candidate");
        var (author, _) = await fixture.User("Candidate");
        var (follower, followerId) = await fixture.User("Candidate");
        var (admin, _) = await fixture.User("Admin");

        // Disable recruitment notifications for candidate
        var pref1 = await candidate.PutAsJsonAsync("/api/account/profile", new AccountProfileUpdateRequest("Muted Candidate", null, false, true));
        Assert.Equal(HttpStatusCode.OK, pref1.StatusCode);

        // Disable community notifications for follower
        var pref2 = await follower.PutAsJsonAsync("/api/account/profile", new AccountProfileUpdateRequest("Muted Follower", null, true, false));
        Assert.Equal(HttpStatusCode.OK, pref2.StatusCode);

        // Community: author creates profile & published post, follower follows author
        await author.PutAsJsonAsync("/api/community/me", new ProfileEdit("auth_" + Guid.NewGuid().ToString("N")[..6], "Author", "Bio", null, true, 0));
        await follower.PutAsJsonAsync("/api/community/me", new ProfileEdit("foll_" + Guid.NewGuid().ToString("N")[..6], "Follower", "Bio", null, true, 0));
        var authorMe = await author.GetFromJsonAsync<JsonElement>("/api/auth/me");
        var authorId = Guid.Parse(authorMe.GetProperty("userId").GetString()!);

        await follower.PutAsync($"/api/community/profiles/{authorId}/follow", null);
        var postRes = await author.PostAsJsonAsync("/api/community/posts", new PostEdit(
            "Bài viết kiểm tra thông báo cộng đồng", "Tóm tắt bài viết",
            "Nội dung bài viết đủ dài để qua kiểm tra độ dài tối thiểu trên hệ thống.",
            "technical", ["dotnet"], null, true, 0));
        Assert.Equal(HttpStatusCode.Created, postRes.StatusCode);
        var post = (await postRes.Content.ReadFromJsonAsync<SocialPost>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/community/posts/{post.Id}/state", new ModerationAction("approve", "Approved", post.RowVersion))).StatusCode);

        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            Assert.Equal(0, await db.CommunityNotices.CountAsync(x => x.RecipientId == followerId));
            Assert.Equal(0, await db.Notifications.CountAsync(x => x.RecipientUserId == candidateId));
        }
    }

    [Fact]
    public async Task AiQuotaEndpointReturnsPeriodAndConcurrencyLimits()
    {
        var (candidate, uid) = await fixture.User("Candidate");
        var file = await FoundationFixture.Upload(candidate);
        var versionId = file.GetProperty("versionId").GetGuid();
        _ = await fixture.Enqueue(uid, versionId);

        var quotaRes = await candidate.GetAsync("/api/account/ai-quota");
        Assert.Equal(HttpStatusCode.OK, quotaRes.StatusCode);
        var quota = await quotaRes.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(quota.GetProperty("enabled").GetBoolean());
        Assert.Equal("Fake", quota.GetProperty("provider").GetString());
        Assert.Equal("test_credit", quota.GetProperty("unit").GetString());
        Assert.Equal("Gói Cơ bản", quota.GetProperty("planLabel").GetString());
        Assert.Equal(200m, quota.GetProperty("limitAmount").GetDecimal());
        Assert.Equal(3, quota.GetProperty("maxActiveJobs").GetInt32());
        Assert.True(quota.GetProperty("reservedAmount").GetDecimal() >= 0m);
        Assert.True(quota.GetProperty("periodEnd").GetDateTimeOffset() > quota.GetProperty("periodStart").GetDateTimeOffset());
    }

    [Fact]
    public async Task LogoutOtherDevicesInvalidatesOtherSessionsWhileKeepingCurrentSession()
    {
        const string password = "AccountSettings!Password2026";
        var (current, uid) = await fixture.User("Candidate", password: password);
        string email;
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            email = (await db.Users.AsNoTracking().SingleAsync(x => x.Id == uid)).Email!;
        }

        using var otherDevice = fixture.App.CreateClient();
        await FoundationFixture.Csrf(otherDevice);
        var login = await otherDevice.PostAsJsonAsync("/api/auth/login", new { email, password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await otherDevice.GetAsync("/api/account/profile")).StatusCode);

        var logoutOthers = await current.PostAsync("/api/auth/logout-other-devices", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutOthers.StatusCode);

        // Current session remains valid
        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/api/account/profile")).StatusCode);
        // Other device session is revoked
        Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/account/profile")).StatusCode);
    }
}
