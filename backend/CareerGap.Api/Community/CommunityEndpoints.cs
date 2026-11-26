using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkiaSharp;

namespace CareerGap.Api.Community;

public static class CommunityEndpoints
{
    public static void MapCommunity(this WebApplication app)
    {
        var g = app.MapGroup("/api/community");
        g.AddEndpointFilter(async (context, next) => {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (CommunityError e) { return Results.Problem(statusCode: e.Status, title: e.Code); }
            catch (DbUpdateConcurrencyException) { return Results.Problem(statusCode: 409, title: "community_conflict"); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) { return Results.Problem(statusCode: 409, title: "community_conflict"); }
        });
        g.MapGet("/categories", () => Results.Ok(CommunityService.Categories));
        g.MapGet("/posts", async (string? mode, string? q, string? category, string? tag, string? tags, string? sort, Guid? author,
            int? page, HttpContext http, CommunityService service, CancellationToken ct) => {
            var uid = CommunityService.User(http.User);
            if ((mode is null or "discover") && !string.IsNullOrWhiteSpace(q))
            {
                var viewer = uid?.ToString() ?? http.Request.Cookies["CareerGap.CommunityViewer"];
                if (!Guid.TryParse(viewer, out _)) { viewer = Guid.NewGuid().ToString(); http.Response.Cookies.Append("CareerGap.CommunityViewer", viewer, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = http.Request.IsHttps, MaxAge = TimeSpan.FromDays(1), Path = "/api/community" }); }
                await service.RecordSearchAsync(q, viewer!, ct);
            }
            return Results.Ok(await service.Feed(uid, mode, q, category, tag, sort, author, page ?? 1, ct, tags));
        });
        g.MapGet("/trending-searches", async (string? q, CommunityService service, CancellationToken ct) =>
        {
            if (q?.Length > 80) return Results.Problem(statusCode: 400, title: "invalid_community_query");
            return Results.Ok(await service.TrendingSearchesAsync(q, ct));
        });
        g.MapGet("/suggestions", async (string? q, AppDb db, CancellationToken ct) => {
            if (q?.Length > 80) return Results.Problem(statusCode: 400, title: "invalid_community_query");
            var term = q?.Trim().ToLowerInvariant() ?? "";
            return Results.Ok(await db.CommunityPostTags.Where(t => t.Tag.Contains(term) && db.CommunityPosts.Any(p => p.Id == t.PostId && p.Status == "published" && db.Users.Any(u => u.Id == p.AuthorId && u.AccountStatus == "active")))
                .GroupBy(t => t.Tag).OrderByDescending(t => t.Count()).ThenBy(t => t.Key).Take(8).Select(t => t.Key).ToArrayAsync(ct));
        });
        g.MapGet("/posts/{id:guid}", async (Guid id, HttpContext http, AppDb db, CommunityService service, CancellationToken ct) => {
            var uid = CommunityService.User(http.User); var p = await service.Read(id, uid, http.User.IsInRole("Admin"), ct);
            if (p.Status == "published") {
                var viewer = uid?.ToString() ?? http.Request.Cookies["CareerGap.CommunityViewer"];
                if (!Guid.TryParse(viewer, out _)) { viewer = Guid.NewGuid().ToString(); http.Response.Cookies.Append("CareerGap.CommunityViewer", viewer, new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = http.Request.IsHttps, MaxAge = TimeSpan.FromDays(1), Path = "/api/community" }); }
                var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(viewer!)));
                var day = DateOnly.FromDateTime(DateTime.UtcNow);
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO community_views (post_id, viewer, day) VALUES ({id}, {hash}, {day}) ON CONFLICT DO NOTHING", ct);
            }
            var result = await service.Posts(service.Accessible(uid, http.User.IsInRole("Admin")).Where(x => x.Id == id), uid, true).SingleOrDefaultAsync(ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        g.MapGet("/by-slug/{slug}", async (string slug, HttpContext http, AppDb db, CommunityService service, CancellationToken ct) => {
            var id = await db.CommunityPosts.Where(x => x.Slug == slug).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            if (id is null) return Results.NotFound(); await service.Read(id.Value, CommunityService.User(http.User), http.User.IsInRole("Admin"), ct);
            return Results.Ok(new { id });
        });
        g.MapGet("/people", async (string? q, AppDb db, ClaimsPrincipal user, CancellationToken ct) => {
            if (q?.Length > 80) return Results.Problem(statusCode: 400, title: "invalid_community_query"); var term = q?.Trim().ToLowerInvariant() ?? "";
            return Results.Ok(await db.CommunityProfiles.Where(p => (p.Handle.Contains(term) || p.DisplayName.ToLower().Contains(term)) && db.Users.Any(u => u.Id == p.UserId && u.AccountStatus == "active"))
                .OrderBy(p => p.Handle).Take(20).Select(p => new { p.UserId, p.Handle, p.DisplayName, p.AvatarId }).ToArrayAsync(ct));
        });
        g.MapGet("/profiles/{id:guid}", async (Guid id, ClaimsPrincipal user, CommunityService service, CancellationToken ct) =>
            await service.Profile(id, CommunityService.User(user), ct) is { } profile ? Results.Ok(profile) : Results.NotFound());
        g.MapGet("/profiles/{id:guid}/connections", async (Guid id, string? direction, int? page, AppDb db, CommunityService service, CancellationToken ct) => {
            if (await service.Profile(id, null, ct) is null) return Results.NotFound();
            if (direction is not ("followers" or "following") || page is < 1 or > 10000) return Results.Problem(statusCode: 400, title: "invalid_community_query");
            var ids = direction == "followers" ? db.CommunityFollows.Where(x => x.FollowingId == id).Select(x => x.FollowerId) : db.CommunityFollows.Where(x => x.FollowerId == id).Select(x => x.FollowingId);
            var query = db.CommunityProfiles.Where(p => ids.Contains(p.UserId) && db.Users.Any(u => u.Id == p.UserId && u.AccountStatus == "active"));
            return Results.Ok(new { items = await query.OrderBy(p => p.Handle).Skip(((page ?? 1) - 1) * 20).Take(20).Select(p => new { p.UserId, p.Handle, p.DisplayName, p.AvatarId }).ToArrayAsync(ct), total = await query.CountAsync(ct) });
        });
        g.MapGet("/me", async (ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(new { profile = await service.Profile(CommunityService.User(user)!.Value, CommunityService.User(user), ct) })).RequireAuthorization();
        g.MapPut("/me", async (ProfileEdit input, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(await service.SaveProfile(CommunityService.User(user)!.Value, input, ct))).RequireAuthorization().RequireRateLimiting("community");
        g.MapPost("/posts", async (PostEdit input, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Created("/api/community/posts", await service.SavePost(CommunityService.User(user)!.Value, null, input, ct))).RequireAuthorization(p => p.RequireRole("Candidate", "Recruiter")).RequireRateLimiting("community");
        g.MapPut("/posts/{id:guid}", async (Guid id, PostEdit input, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(await service.SavePost(CommunityService.User(user)!.Value, id, input, ct))).RequireAuthorization(p => p.RequireRole("Candidate", "Recruiter")).RequireRateLimiting("community");
        g.MapPost("/posts/{id:guid}/state", async (Guid id, ModerationAction input, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => {
            await service.ChangePost(id, CommunityService.User(user)!.Value, user.IsInRole("Admin"), input.Action ?? "", input.RowVersion, input.Reason, ct); return Results.NoContent();
        }).RequireAuthorization().RequireRateLimiting("community");
        g.MapGet("/posts/{id:guid}/comments", async (Guid id, int? page, CommunityService service, CancellationToken ct) => Results.Ok(await service.Comments(id, page ?? 1, ct)));
        g.MapPost("/posts/{id:guid}/comments", async (Guid id, CommentEdit input, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(new { id = await service.SaveComment(CommunityService.User(user)!.Value, id, null, input, ct) })).RequireAuthorization().RequireRateLimiting("community");
        g.MapPut("/posts/{id:guid}/comments/{commentId:guid}", async (Guid id, Guid commentId, CommentEdit input, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(new { id = await service.SaveComment(CommunityService.User(user)!.Value, id, commentId, input, ct) })).RequireAuthorization().RequireRateLimiting("community");
        g.MapDelete("/posts/{id:guid}/comments/{commentId:guid}", async (Guid id, Guid commentId, long rowVersion, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => { await service.DeleteComment(CommunityService.User(user)!.Value, user.IsInRole("Admin"), id, commentId, rowVersion, ct); return Results.NoContent(); }).RequireAuthorization().RequireRateLimiting("community");
        g.MapPut("/posts/{id:guid}/vote", async (Guid id, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(await service.React(CommunityService.User(user)!.Value, id, true, false, ct))).RequireAuthorization().RequireRateLimiting("community");
        g.MapDelete("/posts/{id:guid}/vote", async (Guid id, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(await service.React(CommunityService.User(user)!.Value, id, false, false, ct))).RequireAuthorization().RequireRateLimiting("community");
        g.MapPut("/posts/{id:guid}/bookmark", async (Guid id, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(await service.React(CommunityService.User(user)!.Value, id, true, true, ct))).RequireAuthorization().RequireRateLimiting("community");
        g.MapDelete("/posts/{id:guid}/bookmark", async (Guid id, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(await service.React(CommunityService.User(user)!.Value, id, false, true, ct))).RequireAuthorization().RequireRateLimiting("community");
        g.MapPut("/profiles/{id:guid}/follow", async (Guid id, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(await service.Follow(CommunityService.User(user)!.Value, id, true, ct))).RequireAuthorization().RequireRateLimiting("community");
        g.MapDelete("/profiles/{id:guid}/follow", async (Guid id, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(await service.Follow(CommunityService.User(user)!.Value, id, false, ct))).RequireAuthorization().RequireRateLimiting("community");
        g.MapPost("/reports", async (ReportCreate input, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => Results.Ok(new { id = await service.Report(CommunityService.User(user)!.Value, input, ct) })).RequireAuthorization().RequireRateLimiting("community");
        g.MapGet("/notifications", async (bool? unread, int? page, ClaimsPrincipal user, AppDb db, CancellationToken ct) => {
            if (page is < 1 or > 10000) return Results.Problem(statusCode: 400, title: "invalid_pagination"); var uid = CommunityService.User(user)!.Value;
            var q = db.CommunityNotices.Where(x => x.RecipientId == uid);
            if (unread == true) q = q.Where(x => x.ReadAt == null);
            return Results.Ok(new { items = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip(((page ?? 1) - 1) * 20).Take(20)
                .Select(x => new { x.Id, x.Kind, x.Message, x.PostId, x.ActorId, x.CreatedAt, x.ReadAt,
                    available = x.PostId != null && db.CommunityPosts.Any(p => p.Id == x.PostId && p.Status == "published" && db.Users.Any(u => u.Id == p.AuthorId && u.AccountStatus == "active")) }).ToArrayAsync(ct), total = await q.CountAsync(ct), unread = await db.CommunityNotices.CountAsync(x => x.RecipientId == uid && x.ReadAt == null, ct) });
        }).RequireAuthorization();
        g.MapPut("/notifications/read-all", async (ClaimsPrincipal user, AppDb db, TimeProvider clock, CancellationToken ct) => { var uid = CommunityService.User(user); await db.CommunityNotices.Where(x => x.RecipientId == uid && x.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, clock.GetUtcNow()), ct); return Results.NoContent(); }).RequireAuthorization();
        g.MapPut("/notifications/{id:guid}/read", async (Guid id, ClaimsPrincipal user, AppDb db, TimeProvider clock, CancellationToken ct) => {
            var uid = CommunityService.User(user); var notice = await db.CommunityNotices.SingleOrDefaultAsync(x => x.Id == id && x.RecipientId == uid, ct);
            if (notice is null) return Results.NotFound(); notice.ReadAt ??= clock.GetUtcNow(); await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization();
        MapImages(g); MapAdmin(g);
        g.MapGet("/events", StreamEvents).RequireAuthorization().RequireRateLimiting("community-stream");
    }
    private static void MapAdmin(RouteGroupBuilder g)
    {
        var a = g.MapGroup("/admin").RequireAuthorization(p => p.RequireRole("Admin"));
        a.MapGet("/posts", async (string? status, int? page, AppDb db, CommunityService service, CancellationToken ct) => {
            if (page is < 1 or > 10000 || status is not (null or "pending" or "published" or "hidden" or "deleted")) return Results.Problem(statusCode: 400, title: "invalid_community_query");
            var q = db.CommunityPosts.Where(x => x.Status != "draft"); if (status is not null) q = q.Where(x => x.Status == status);
            return Results.Ok(new { items = await service.Posts(q.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip(((page ?? 1) - 1) * 20).Take(20), null, true).ToArrayAsync(ct), total = await q.CountAsync(ct) });
        });
        a.MapGet("/reports",CommunityReportQueries.List);
        a.MapGet("/reports/{id:guid}",CommunityReportQueries.Detail);
        a.MapPost("/reports/{id:guid}", async (Guid id, ModerationAction input, ClaimsPrincipal user, CommunityService service, CancellationToken ct) => { await service.ModerateReport(CommunityService.User(user)!.Value, id, input, ct); return Results.NoContent(); });
        a.MapGet("/stats", async (AppDb db, CancellationToken ct) => Results.Ok(new { posts = await db.CommunityPosts.CountAsync(ct), pending = await db.CommunityPosts.CountAsync(x => x.Status == "pending", ct), comments = await db.CommunityComments.CountAsync(x => !x.IsDeleted, ct), members = await db.CommunityProfiles.CountAsync(ct), reports = await db.CommunityReports.CountAsync(x => x.Status == "open" || x.Status == "reviewed", ct) }));
    }
    private static void MapImages(RouteGroupBuilder g)
    {
        g.MapGet("/images/mine", async (ClaimsPrincipal user, AppDb db, CancellationToken ct) => Results.Ok(await db.CommunityImages.Where(x => x.OwnerId == CommunityService.User(user)).OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.CreatedAt }).Take(30).ToArrayAsync(ct))).RequireAuthorization();
        g.MapPost("/images", async (HttpContext http, AppDb db, IFileStorage storage, CancellationToken ct) => {
            var uid = CommunityService.User(http.User)!.Value;
            if (!http.Request.HasFormContentType) return Results.Problem(statusCode: 400, title: "community_image_invalid"); var form = await http.Request.ReadFormAsync(ct); var file = form.Files.GetFile("file");
            if (file is null || file.Length is <= 0 or > 2 * 1024 * 1024) return Results.Problem(statusCode: 400, title: "community_image_invalid");
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var account = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={uid} FOR UPDATE").AsNoTracking().SingleAsync(ct);
            if (account.AccountStatus != "active") return Results.Unauthorized();
            if (await db.CommunityImages.CountAsync(x => x.OwnerId == uid, ct) >= 30) return Results.Problem(statusCode: 422, title: "community_image_limit");
            byte[] output;
            try {
                using var input = new MemoryStream(); await file.CopyToAsync(input, ct); input.Position = 0;
                using var codec = SKCodec.Create(input);
                if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg) || codec.FrameCount > 1 ||
                    codec.Info.Width < 1 || codec.Info.Height < 1 || (long)codec.Info.Width * codec.Info.Height > 8_000_000) throw new InvalidDataException();
                using var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
                if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success) throw new InvalidDataException();
                var ratio = Math.Min(1d, 1200d / Math.Max(bitmap.Width, bitmap.Height));
                using var resized = new SKBitmap(new SKImageInfo(Math.Max(1, (int)(bitmap.Width * ratio)), Math.Max(1, (int)(bitmap.Height * ratio)), SKColorType.Rgba8888, SKAlphaType.Premul));
                if (!bitmap.ScalePixels(resized, new SKSamplingOptions(SKFilterMode.Linear))) throw new InvalidDataException();
                using var clean = SKImage.FromBitmap(resized); using var encoded = clean.Encode(SKEncodedImageFormat.Png, 100);
                output = encoded.ToArray(); if (output.Length > 4 * 1024 * 1024) throw new InvalidDataException();
            } catch (Exception e) when (e is InvalidDataException or NotSupportedException) { return Results.Problem(statusCode: 400, title: "community_image_invalid"); }
            var row = new CommunityImage { OwnerId = uid, ObjectKey = Guid.NewGuid().ToString("N"), ContentType = "image/png", Provider = storage.Provider };
            await storage.PutAsync(row.ObjectKey, output, row.ContentType, ct);
            try { db.CommunityImages.Add(row); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
            catch { await storage.DeleteAsync(row.ObjectKey, CancellationToken.None); throw; }
            return Results.Created("/api/community/images/" + row.Id, new { row.Id });
        }).WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(2 * 1024 * 1024 + 64 * 1024)).RequireAuthorization().RequireRateLimiting("community");
        g.MapGet("/images/{id:guid}", async (Guid id, HttpContext http, AppDb db, IFileStorage storage, CancellationToken ct) => {
            var uid = CommunityService.User(http.User); var image = await db.CommunityImages.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (image is null || !await db.Users.AnyAsync(x => x.Id == image.OwnerId && x.AccountStatus == "active", ct)) return Results.NotFound();
            var own = image.OwnerId == uid;
            var imagePath = "/api/community/images/" + id;
            var publicAvatar = await db.CommunityProfiles.AnyAsync(x => x.AvatarId == id, ct);
            var publication = await db.CommunityPosts.AnyAsync(x => (x.ThumbnailId == id || x.Body.Contains(imagePath)) && (x.Status == "published" || http.User.IsInRole("Admin") && x.Status != "draft"), ct);
            if (!own && !publicAvatar && !publication) return Results.NotFound();
            var bytes = await storage.ReadAsync(image.ObjectKey, ct);
            // Recheck after storage I/O: a post may have been hidden or its owner suspended meanwhile.
            if (!await db.CommunityImages.AnyAsync(x => x.Id == id && db.Users.Any(u => u.Id == x.OwnerId && u.AccountStatus == "active") &&
                (x.OwnerId == uid || db.CommunityProfiles.Any(p => p.AvatarId == id) || db.CommunityPosts.Any(p => (p.ThumbnailId == id || p.Body.Contains(imagePath)) &&
                (p.Status == "published" || http.User.IsInRole("Admin") && p.Status != "draft"))), ct)) return Results.NotFound();
            http.Response.Headers.XContentTypeOptions = "nosniff"; return Results.File(bytes, image.ContentType);
        });
        g.MapDelete("/images/{id:guid}", async (Guid id, ClaimsPrincipal user, AppDb db, IFileStorage storage, CancellationToken ct) => {
            var uid = CommunityService.User(user);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var account = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={uid} FOR UPDATE").AsNoTracking().SingleAsync(ct);
            if (account.AccountStatus != "active") return Results.Unauthorized();
            var image = await db.CommunityImages.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == uid, ct);
            if (image is null) return Results.NotFound();
            var imagePath = "/api/community/images/" + id;
            if (await db.Companies.AnyAsync(x=>x.LogoImageId==id,ct) || await db.CommunityPosts.AnyAsync(x => x.ThumbnailId == id || x.Body.Contains(imagePath), ct) || await db.CommunityProfiles.AnyAsync(x => x.AvatarId == id, ct)) return Results.Problem(statusCode: 409, title: "community_image_in_use");
            await storage.DeleteAsync(image.ObjectKey, ct); db.CommunityImages.Remove(image); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
        }).RequireAuthorization();
    }
    private static async Task StreamEvents(HttpContext http, IServiceScopeFactory scopes, CancellationToken ct)
    {
        var uid = CommunityService.User(http.User)!.Value;
        var ticket = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var expires = ticket.Properties?.ExpiresUtc ?? DateTimeOffset.UtcNow;
        http.Response.ContentType = "text/event-stream"; http.Response.Headers.CacheControl = "no-store"; http.Response.Headers["X-Accel-Buffering"] = "no";
        string? last = null;
        try {
            while (!ct.IsCancellationRequested && DateTimeOffset.UtcNow < expires) {
                using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                if (!await db.Users.AnyAsync(x => x.Id == uid && x.AccountStatus == "active", ct) || await scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>().ValidateSecurityStampAsync(http.User) is null) break;
                var unread = await db.CommunityNotices.CountAsync(x => x.RecipientId == uid && x.ReadAt == null, ct);
                var publicPosts = scope.ServiceProvider.GetRequiredService<CommunityService>().Published();
                var posts = await publicPosts.MaxAsync(x => (DateTimeOffset?)x.UpdatedAt, ct);
                var count = await publicPosts.CountAsync(ct);
                var members = await db.CommunityProfiles.CountAsync(p => db.Users.Any(u => u.Id == p.UserId && u.AccountStatus == "active"), ct);
                var comments = await db.CommunityComments.Where(x => db.CommunityPosts.Any(p => p.Id == x.PostId && p.Status == "published")).MaxAsync(x => (DateTimeOffset?)x.UpdatedAt, ct);
                var signal=await CareerGap.Api.Applications.ApplicationEndpoints.NotificationSignal(db,uid,http.User.IsInRole("Recruiter"),ct);
                var recruitment=new{total=signal.Total,unread=signal.Unread,revision=signal.Revision};
                var value = JsonSerializer.Serialize(new { unread, posts, comments, count, members, recruitment });
                if (value != last) { await http.Response.WriteAsync("event: update\ndata: " + value + "\nretry: 5000\n\n", ct); last = value; }
                else await http.Response.WriteAsync(": keep-alive\n\n", ct);
                await http.Response.Body.FlushAsync(ct); await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        } catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }
}
