using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CareerGap.Api.Community;

public sealed partial class CommunityService(AppDb db, TimeProvider clock)
{
    public static readonly string[] Categories = ["career", "technical", "interview", "questions"];
    public static Guid? User(ClaimsPrincipal p) => Guid.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static void Ensure(bool condition, int status, string code) { if (!condition) throw new CommunityError(status, code); }
    public IQueryable<CommunityPost> Published() => db.CommunityPosts.Where(p => p.Status == "published" &&
        db.Users.Any(u => u.Id == p.AuthorId && u.AccountStatus == "active") && db.CommunityProfiles.Any(x => x.UserId == p.AuthorId));
    public IQueryable<CommunityPost> Accessible(Guid? viewer, bool admin) => db.CommunityPosts.Where(p =>
        db.Users.Any(u => u.Id == p.AuthorId && u.AccountStatus == "active") &&
        (p.AuthorId == viewer || p.Status == "published" || admin && p.Status != "draft"));
    public async Task<CommunityPost> Read(Guid id, Guid? viewer, bool admin, CancellationToken ct) =>
        await Accessible(viewer, admin).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new CommunityError(404, "community_not_found");
    private async Task<CommunityPost> LockPost(Guid id, CancellationToken ct) =>
        await db.CommunityPosts.FromSqlInterpolated($"SELECT * FROM community_posts WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct)
        ?? throw new CommunityError(404, "community_not_found");
    private async Task LockUser(Guid id, CancellationToken ct)
    {
        var u = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct);
        Ensure(u?.AccountStatus == "active", 401, "community_account_unavailable");
    }
    private async Task RequireProfile(Guid id, CancellationToken ct) => Ensure(await db.CommunityProfiles.AnyAsync(x => x.UserId == id, ct), 422, "community_profile_required");
    public async Task<SocialProfile?> Profile(Guid id, Guid? viewer, CancellationToken ct)
    {
        var profile = await (from p in db.CommunityProfiles.AsNoTracking()
                      where p.UserId == id && db.Users.Any(u => u.Id == id && u.AccountStatus == "active")
                      select new SocialProfile(p.UserId, p.Handle, p.DisplayName, p.Bio, p.AvatarId, p.AllowMentions, p.RowVersion,
                          db.CommunityFollows.Count(x => x.FollowingId == id && db.Users.Any(u => u.Id == x.FollowerId && u.AccountStatus == "active")),
                          db.CommunityFollows.Count(x => x.FollowerId == id && db.Users.Any(u => u.Id == x.FollowingId && u.AccountStatus == "active")),
                          db.CommunityPosts.Count(x => x.AuthorId == id && x.Status == "published"), viewer != null && db.CommunityFollows.Any(x => x.FollowerId == viewer && x.FollowingId == id), p.ShowCareer, null)).SingleOrDefaultAsync(ct);
        if (profile is null) return null;
        var career = await Career(id, profile.ShowCareer, ct);
        if (!await db.Users.AnyAsync(u=>u.Id==id && u.AccountStatus=="active",ct)) return null;
        if(profile.ShowCareer && !await db.CommunityProfiles.AnyAsync(p=>p.UserId==id && p.ShowCareer,ct))
            return profile with {ShowCareer=false, Career=career with {Headline=null, DesiredRole=null, Location=null, ExperienceYears=null, Skills=[], Company=null}};
        return profile with {Career=career};
    }
    public async Task<SocialProfile> SaveProfile(Guid uid, ProfileEdit input, CancellationToken ct)
    {
        var handle = input.Handle?.Trim().ToLowerInvariant() ?? "";
        Ensure(Regex.IsMatch(handle, "^[a-z][a-z0-9_]{2,23}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) &&
            input.DisplayName?.Trim().Length is >= 2 and <= 100 && (input.Bio?.Length ?? 0) <= 1000 && input.RowVersion >= 0, 400, "invalid_community_profile");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockUser(uid, ct);
        await OwnImage(uid, input.AvatarId, ct);
        var profile = await db.CommunityProfiles.SingleOrDefaultAsync(x => x.UserId == uid, ct);
        Ensure(profile is null ? input.RowVersion == 0 : profile.RowVersion == input.RowVersion, 409, "community_conflict");
        Ensure(profile is null || profile.Handle == handle, 400, "community_handle_immutable");
        Ensure(!await db.CommunityProfiles.AnyAsync(x => x.Handle == handle && x.UserId != uid, ct), 409, "community_handle_taken");
        if (profile is null) { profile = new() { UserId = uid, Handle = handle }; db.CommunityProfiles.Add(profile); }
        profile.DisplayName = input.DisplayName!.Trim(); profile.Bio = input.Bio?.Trim() ?? "";
        if(input.ShowCareer is {} showCareer) profile.ShowCareer=showCareer;
        profile.AvatarId = input.AvatarId; profile.AllowMentions = input.AllowMentions; profile.RowVersion++; profile.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return (await Profile(uid, uid, ct))!;
    }
    private async Task OwnImage(Guid uid, Guid? id, CancellationToken ct)
    { if (id is not null) Ensure(await db.CommunityImages.AnyAsync(x => x.Id == id && x.OwnerId == uid, ct), 400, "community_image_not_owned"); }
    public async Task<SocialPage<SocialPost>> Feed(Guid? uid, string? mode, string? q, string? category, string? tag,
        string? sort, Guid? author, int page, CancellationToken ct, string? tags = null)
    {
        Ensure(page is >= 1 and <= 10000 && (q?.Length ?? 0) <= 120 && (tag?.Length ?? 0) <= 40 && (tags?.Length ?? 0) <= 400, 400, "invalid_community_query");
        mode ??= "discover"; sort ??= "newest";
        Ensure(new[] { "discover", "following", "saved", "mine" }.Contains(mode) && new[] { "newest", "trending", "views" }.Contains(sort), 400, "invalid_community_query");
        Ensure(mode == "discover" || uid is not null, 401, "community_login_required");
        var query = mode == "mine" ? db.CommunityPosts.Where(x => x.AuthorId == uid) : Published();
        if (mode == "following") query = query.Where(x => x.AuthorId == uid || db.CommunityFollows.Any(f => f.FollowerId == uid && f.FollowingId == x.AuthorId));
        if (mode == "saved") query = query.Where(x => db.CommunityBookmarks.Any(b => b.PostId == x.Id && b.UserId == uid));
        if (author is not null) query = query.Where(x => x.AuthorId == author);
        if (!string.IsNullOrWhiteSpace(q)) { var term = q.Trim().ToLowerInvariant(); query = query.Where(x => x.Title.ToLower().Contains(term) || x.Summary.ToLower().Contains(term) || x.Body.ToLower().Contains(term)); }
        if (!string.IsNullOrEmpty(category)) query = query.Where(x => x.Category == category);
        var selectedTags=(string.IsNullOrWhiteSpace(tags)?Array.Empty<string>():tags.Split(',')).Concat(string.IsNullOrWhiteSpace(tag)?[]:[tag])
            .Select(t=>t.Trim().ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray();
        Ensure(selectedTags.Length<=8 && selectedTags.All(t=>t.Length<=40 && Regex.IsMatch(t,"^\\.?[\\p{L}\\p{N}][\\p{L}\\p{N}+#._ -]{0,39}$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))),400,"invalid_community_tags");
        if(selectedTags.Length>0)query=query.Where(x=>db.CommunityPostTags.Any(a=>a.PostId==x.Id&&selectedTags.Contains(a.Tag)));
        var total = await query.CountAsync(ct);
        var matchOrder=selectedTags.Length>0?query.OrderByDescending(x=>db.CommunityPostTags.Count(a=>a.PostId==x.Id&&selectedTags.Contains(a.Tag))):null;
        var ordered=sort switch {
            "trending" => matchOrder?.ThenByDescending(x=>db.CommunityReactions.Count(r=>r.PostId==x.Id)+db.CommunityComments.Count(c=>c.PostId==x.Id&&!c.IsDeleted))
                ??query.OrderByDescending(x=>db.CommunityReactions.Count(r=>r.PostId==x.Id)+db.CommunityComments.Count(c=>c.PostId==x.Id&&!c.IsDeleted)),
            "views" => matchOrder?.ThenByDescending(x=>db.CommunityViews.Count(v=>v.PostId==x.Id))??query.OrderByDescending(x=>db.CommunityViews.Count(v=>v.PostId==x.Id)),
            _ => matchOrder??query.OrderByDescending(x=>x.PublishedAt??x.CreatedAt)
        };
        query=sort=="newest"?(matchOrder is null?ordered:ordered.ThenByDescending(x=>x.PublishedAt??x.CreatedAt)).ThenByDescending(x=>x.Id)
            :ordered.ThenByDescending(x=>x.PublishedAt).ThenByDescending(x=>x.Id);
        return new(await Posts(query.Skip((page - 1) * 12).Take(12), uid, false).ToArrayAsync(ct), page, 12, total);
    }
    public async Task RecordSearchAsync(string? rawQuery, string viewerIdentity, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawQuery) || string.IsNullOrWhiteSpace(viewerIdentity)) return;
        var normalized = Regex.Replace(rawQuery.Trim().ToLowerInvariant(), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        if (normalized.Length is < 2 or > 40) return;
        if (normalized.Split(' ').Length > 4) return;
        if (normalized.Contains('@') || normalized.Contains("http") || normalized.Contains("www.")) return;
        if (Regex.IsMatch(normalized, @"\d{6,}", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))) return;
        if (!Regex.IsMatch(normalized, @"^[\p{L}\p{N} .+#_-]+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50))) return;

        // Privacy guard: only record terms that match at least one published post title/summary.
        var hasPublicMatch = await Published().AnyAsync(p => p.Title.ToLower().Contains(normalized) || p.Summary.ToLower().Contains(normalized), ct);
        if (!hasPublicMatch) return;

        var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(viewerIdentity)));
        var day = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO community_searchs (term, viewer, day) VALUES ({normalized}, {hash}, {day}) ON CONFLICT DO NOTHING", ct);
    }

    public async Task<object[]> TrendingSearchesAsync(string? q, CancellationToken ct)
    {
        var term = q?.Trim().ToLowerInvariant() ?? "";
        var query = db.CommunitySearches.AsNoTracking();
        if (!string.IsNullOrEmpty(term)) query = query.Where(x => x.Term.Contains(term));
        return await query
            .GroupBy(x => x.Term)
            .Select(g => new { term = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ThenBy(x => x.term)
            .Take(8)
            .Cast<object>()
            .ToArrayAsync(ct);
    }

    public IQueryable<SocialPost> Posts(IQueryable<CommunityPost> query, Guid? uid, bool body) => query.Select(p => new SocialPost(p.Id, p.AuthorId,
        db.CommunityProfiles.Where(x => x.UserId == p.AuthorId).Select(x => x.DisplayName).FirstOrDefault() ?? "Thành viên đã xóa",
        db.CommunityProfiles.Where(x => x.UserId == p.AuthorId).Select(x => x.Handle).FirstOrDefault() ?? "",
        db.CommunityProfiles.Where(x => x.UserId == p.AuthorId).Select(x => x.AvatarId).FirstOrDefault(), p.Title, p.Slug, p.Summary,
        body ? p.Body : null, p.Category, db.CommunityPostTags.Where(t => t.PostId == p.Id).OrderBy(t => t.Tag).Select(t => t.Tag).ToArray(),
        p.ThumbnailId, p.Status, p.RowVersion, p.CreatedAt, p.PublishedAt,
        db.CommunityReactions.Count(x => x.PostId == p.Id && db.Users.Any(u => u.Id == x.UserId && u.AccountStatus == "active")),
        db.CommunityComments.Count(x => x.PostId == p.Id && !x.IsDeleted && db.Users.Any(u => u.Id == x.AuthorId && u.AccountStatus == "active")),
        db.CommunityBookmarks.Count(x => x.PostId == p.Id), db.CommunityViews.Count(x => x.PostId == p.Id),
        uid != null && db.CommunityReactions.Any(x => x.PostId == p.Id && x.UserId == uid), uid != null && db.CommunityBookmarks.Any(x => x.PostId == p.Id && x.UserId == uid)));
    public async Task<SocialPost> SavePost(Guid uid, Guid? id, PostEdit input, CancellationToken ct)
    {
        Ensure(input.Title?.Trim().Length is >= 5 and <= 200 && input.Body?.Trim().Length is >= 20 and <= 30000 &&
            (input.Summary?.Length ?? 0) <= 500 && input.Category is not null && Categories.Contains(input.Category) && input.RowVersion >= 0 && input.Tags is { Length: <= 8 }, 400, "invalid_community_post");
        var tags = input.Tags!.Select(x => x?.Trim().ToLowerInvariant() ?? "").Distinct().ToArray();
        Ensure(tags.All(x => x.Length <= 40 && Regex.IsMatch(x, "^\\.?[\\p{L}\\p{N}][\\p{L}\\p{N}+#._ -]{0,39}$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))), 400, "invalid_community_tags");
        Ensure(Mentions(input.Body!).Length <= 10, 400, "community_mention_limit");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockUser(uid, ct); await RequireProfile(uid, ct);
        await OwnImage(uid, input.ThumbnailId, ct);
        CommunityPost post;
        if (id is null) {
            Ensure(await db.CommunityPosts.CountAsync(x => x.AuthorId == uid && x.CreatedAt > clock.GetUtcNow().AddDays(-1), ct) < 30, 429, "community_post_limit");
            post = new() { AuthorId = uid, CreatedAt = clock.GetUtcNow() }; post.Slug = Slug(input.Title!) + "-" + post.Id.ToString("N")[..10]; db.CommunityPosts.Add(post);
        } else { post = await LockPost(id.Value, ct); Ensure(post.AuthorId == uid && post.Status != "deleted", 404, "community_not_found"); Ensure(post.RowVersion == input.RowVersion, 409, "community_conflict"); }
        post.Title = input.Title!.Trim(); post.Summary = input.Summary?.Trim() ?? ""; post.Body = input.Body!.Trim(); post.Category = input.Category!;
        post.ThumbnailId = input.ThumbnailId; post.Status = input.Submit ? "pending" : "draft"; post.RowVersion++; post.UpdatedAt = clock.GetUtcNow();
        await db.CommunityPostTags.Where(x => x.PostId == post.Id).ExecuteDeleteAsync(ct);
        foreach (var t in tags) {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO community_tags (name) VALUES ({t}) ON CONFLICT DO NOTHING", ct);
            db.CommunityPostTags.Add(new() { PostId = post.Id, Tag = t });
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Posts(db.CommunityPosts.Where(x => x.Id == post.Id), uid, true).SingleAsync(ct);
    }
    private static string Slug(string title)
    {
        var s = title.Normalize(NormalizationForm.FormD); var chars = s.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        s = new string(chars).Normalize(NormalizationForm.FormC).ToLowerInvariant().Replace('đ', 'd');
        s = Regex.Replace(s, "[^a-z0-9]+", "-", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).Trim('-');
        return s.Length == 0 ? "bai-viet" : s[..Math.Min(180, s.Length)];
    }
    public async Task ChangePost(Guid id, Guid actor, bool admin, string action, long version, string? reason, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockUser(actor, ct); var p = await LockPost(id, ct);
        Ensure(p.RowVersion == version, 409, "community_conflict");
        Ensure(p.AuthorId == actor || admin && p.Status != "draft", 404, "community_not_found");
        if (action is "approve" or "hide" or "restore") Ensure(admin, 403, "community_admin_required");
        if (admin) Ensure(reason?.Trim().Length is >= 3 and <= 500, 400, "community_reason_required");
        var previous = p.Status;
        p.Status = action switch {
            "submit" when p.AuthorId == actor && p.Status is "draft" or "hidden" => "pending",
            "approve" when p.Status == "pending" => "published",
            "hide" when p.Status is "pending" or "published" => "hidden",
            "delete" when p.Status != "deleted" => "deleted",
            "restore" when p.Status == "deleted" => "hidden",
            _ => throw new CommunityError(409, "community_invalid_transition")
        };
        p.PreviousStatus = previous; p.RowVersion++; p.UpdatedAt = clock.GetUtcNow();
        if (p.Status == "published") { p.PublishedAt ??= clock.GetUtcNow(); await NotifyPublication(p, ct); }
        if (admin) {
            db.AdminAudits.Add(new() { ActorId = actor, TargetId = p.Id, Action = "community_" + action, Reason = reason!.Trim() });
            var label = p.Status switch { "published" => "đã được duyệt đăng", "hidden" => "đã bị ẩn", "deleted" => "đã chuyển vào thùng rác", _ => "đã được khôi phục về trạng thái ẩn" };
            await Notice(p.AuthorId, actor, p.Id, "moderation", "Bài viết của bạn " + label + ". Lý do: " + reason!.Trim()[..Math.Min(400, reason.Trim().Length)], $"moderation:{p.Id}:{p.RowVersion}", ct);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private static string[] Mentions(string text) => Regex.Matches(text, "(?<![\\w@])@([a-z][a-z0-9_]{2,23})\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
        .Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToArray();
    private async Task MentionNotices(Guid actor, Guid post, string text, string source, CancellationToken ct)
    {
        var handles = Mentions(text); Ensure(handles.Length <= 10, 400, "community_mention_limit");
        var targets = await db.CommunityProfiles.Where(x => x.AllowMentions && handles.Contains(x.Handle) && db.Users.Any(u => u.Id == x.UserId && u.AccountStatus == "active")).Select(x => x.UserId).ToArrayAsync(ct);
        foreach (var target in targets) await Notice(target, actor, post, "mention", "Bạn được nhắc tên trong cộng đồng.", $"mention:{source}:{target}", ct);
    }
    private async Task NotifyPublication(CommunityPost p, CancellationToken ct)
    {
        await MentionNotices(p.AuthorId, p.Id, p.Title + " " + p.Body, "post:" + p.Id, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO community_notices (id, recipient_id, actor_id, post_id, kind, message, dedup_key, created_at)
            SELECT gen_random_uuid(), f.follower_id, {p.AuthorId}, {p.Id}, 'new_post', 'Người bạn theo dõi có bài viết mới.',
                'new_post:' || {p.Id.ToString()} || ':' || f.follower_id::text, now()
            FROM community_follows f JOIN "AspNetUsers" u ON u."Id" = f.follower_id
            WHERE f.following_id = {p.AuthorId} AND u."AccountStatus" = 'active'
            ON CONFLICT (dedup_key) DO NOTHING
            """, ct);
    }
    private async Task Notice(Guid recipient, Guid actor, Guid? post, string kind, string message, string key, CancellationToken ct)
    {
        if (recipient == actor || await db.CommunityNotices.AnyAsync(x => x.DedupKey == key, ct)) return;
        db.CommunityNotices.Add(new() { RecipientId = recipient, ActorId = actor, PostId = post, Kind = kind, Message = message, DedupKey = key, CreatedAt = clock.GetUtcNow() });
    }
}
