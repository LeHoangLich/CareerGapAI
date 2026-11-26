using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Community;
using Xunit;

namespace CareerGap.Api.Tests;

public sealed class CommunityBodyImageTests(FoundationFixture fixture) : IClassFixture<FoundationFixture>
{
    [Fact]
    public async Task InlineBodyImageIsReadableAfterPublishAndProtectedFromDeletion()
    {
        var (author, _) = await fixture.User("Candidate");
        var (admin, _) = await fixture.User("Admin");
        await author.PutAsJsonAsync("/api/community/me", new ProfileEdit("bodyimg_user", "Body Img", "Bio", null, true, 0));

        using var bmp = new SkiaSharp.SKBitmap(8, 8);
        bmp.Erase(SkiaSharp.SKColors.Teal);
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var png = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(png.ToArray()), "file", "inline.png");
        var upload = await author.PostAsync("/api/community/images", form);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var imageId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var create = await author.PostAsJsonAsync("/api/community/posts", new PostEdit(
            "Bài viết có ảnh minh họa", "Tóm tắt",
            $"Nội dung bài viết có ảnh nội bộ:\n![Minh họa](/api/community/images/{imageId})\nKết thúc bài.",
            "technical", ["react"], null, true, 0));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var post = (await create.Content.ReadFromJsonAsync<SocialPost>())!;

        // Guest cannot view image before publish, and author cannot delete image while referenced in post body.
        var guest = fixture.App.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/community/images/{imageId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await author.DeleteAsync($"/api/community/images/{imageId}")).StatusCode);

        var approve = await admin.PostAsJsonAsync($"/api/community/posts/{post.Id}/state", new ModerationAction("approve", "Approved", post.RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/community/images/{imageId}")).StatusCode);
    }
}
