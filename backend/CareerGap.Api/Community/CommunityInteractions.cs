using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Community;

public sealed partial class CommunityService
{
    public async Task<object> Follow(Guid actor, Guid target, bool follow, CancellationToken ct)
    {
        Ensure(actor != target, 400, "community_self_follow");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // A stable lock order prevents mutual follows from deadlocking with account deletion.
        var accounts = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\"={actor} OR \"Id\"={target} ORDER BY \"Id\" FOR UPDATE").AsNoTracking().ToArrayAsync(ct);
        Ensure(accounts.Any(x => x.Id == actor && x.AccountStatus == "active"), 401, "community_account_unavailable"); await RequireProfile(actor, ct);
        Ensure(await Profile(target, actor, ct) is not null, 404, "community_not_found");
        var row = await db.CommunityFollows.SingleOrDefaultAsync(x => x.FollowerId == actor && x.FollowingId == target, ct);
        if (follow && row is null) {
            Ensure(await db.CommunityFollows.CountAsync(x => x.FollowerId == actor, ct) < 1000, 422, "community_follow_limit");
            db.CommunityFollows.Add(new() { FollowerId = actor, FollowingId = target, CreatedAt = clock.GetUtcNow() });
            await Notice(target, actor, null, "follow", "Có người bắt đầu theo dõi bạn.", $"follow:{actor}:{target}", ct);
        } else if (!follow && row is not null) db.CommunityFollows.Remove(row);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new { following = follow };
    }
    public async Task<object> React(Guid actor, Guid postId, bool active, bool bookmark, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockUser(actor, ct); var p = await LockPost(postId, ct);
        Ensure(await Published().AnyAsync(x => x.Id == postId, ct), 404, "community_not_found"); await RequireProfile(actor, ct);
        if (bookmark) {
            var b = await db.CommunityBookmarks.SingleOrDefaultAsync(x => x.PostId == postId && x.UserId == actor, ct);
            if (active && b is null) db.CommunityBookmarks.Add(new() { UserId = actor, PostId = postId });
            else if (!active && b is not null) db.CommunityBookmarks.Remove(b);
        } else {
            var r = await db.CommunityReactions.SingleOrDefaultAsync(x => x.PostId == postId && x.UserId == actor, ct);
            if (active && r is null) { db.CommunityReactions.Add(new() { UserId = actor, PostId = postId }); await Notice(p.AuthorId, actor, p.Id, "vote", "Có người thích bài viết của bạn.", $"vote:{p.Id}:{actor}", ct); }
            else if (!active && r is not null) db.CommunityReactions.Remove(r);
        }
        p.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new { active, votes = await db.CommunityReactions.CountAsync(x => x.PostId == postId, ct), bookmarks = await db.CommunityBookmarks.CountAsync(x => x.PostId == postId, ct) };
    }
    public async Task<SocialPage<SocialComment>> Comments(Guid postId, int page, CancellationToken ct)
    {
        Ensure(page is >= 1 and <= 10000, 400, "invalid_community_query");
        Ensure(await Published().AnyAsync(x => x.Id == postId, ct), 404, "community_not_found");
        // Include deleted parent placeholders so replies keep their context; never return deleted content.
        var query = db.CommunityComments.Where(x => x.PostId == postId && Published().Any(p => p.Id == x.PostId));
        var total = await query.CountAsync(ct);
        return new(await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * 30).Take(30).Select(c => new SocialComment(c.Id, c.PostId, c.AuthorId,
            db.CommunityProfiles.Where(x => x.UserId == c.AuthorId && db.Users.Any(u => u.Id == x.UserId && u.AccountStatus == "active")).Select(x => x.DisplayName).FirstOrDefault() ?? "Thành viên đã xóa",
            db.CommunityProfiles.Where(x => x.UserId == c.AuthorId && db.Users.Any(u => u.Id == x.UserId && u.AccountStatus == "active")).Select(x => x.Handle).FirstOrDefault() ?? "",
            db.CommunityProfiles.Where(x => x.UserId == c.AuthorId && db.Users.Any(u => u.Id == x.UserId && u.AccountStatus == "active")).Select(x => x.AvatarId).FirstOrDefault(),
            c.ParentId, c.IsDeleted || !db.Users.Any(u => u.Id == c.AuthorId && u.AccountStatus == "active") ? "Bình luận đã được gỡ." : c.Body,
            c.IsDeleted || !db.Users.Any(u => u.Id == c.AuthorId && u.AccountStatus == "active"), c.RowVersion, c.CreatedAt)).ToArrayAsync(ct), page, 30, total);
    }
    public async Task<Guid> SaveComment(Guid actor, Guid postId, Guid? commentId, CommentEdit input, CancellationToken ct)
    {
        Ensure(input.Body?.Trim().Length is >= 1 and <= 4000 && input.RowVersion >= 0, 400, "invalid_community_comment");
        Ensure(Mentions(input.Body!).Length <= 10, 400, "community_mention_limit");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockUser(actor, ct); var p = await LockPost(postId, ct);
        Ensure(await Published().AnyAsync(x => x.Id == postId, ct), 404, "community_not_found"); await RequireProfile(actor, ct);
        CommunityComment c;
        if (commentId is null) {
            Ensure(await db.CommunityComments.CountAsync(x => x.AuthorId == actor && x.CreatedAt > clock.GetUtcNow().AddMinutes(-1), ct) < 20, 429, "community_comment_limit");
            if (input.ParentId is not null) Ensure(await db.CommunityComments.AnyAsync(x => x.Id == input.ParentId && x.PostId == postId && x.ParentId == null && !x.IsDeleted && db.Users.Any(u => u.Id == x.AuthorId && u.AccountStatus == "active"), ct), 400, "community_reply_invalid");
            c = new() { PostId = postId, AuthorId = actor, ParentId = input.ParentId, CreatedAt = clock.GetUtcNow() }; db.CommunityComments.Add(c);
        } else {
            c = await db.CommunityComments.SingleOrDefaultAsync(x => x.Id == commentId && x.PostId == postId && x.AuthorId == actor && !x.IsDeleted, ct) ?? throw new CommunityError(404, "community_not_found");
            Ensure(c.RowVersion == input.RowVersion && input.ParentId == c.ParentId, 409, "community_conflict");
        }
        c.Body = input.Body!.Trim(); c.RowVersion++; c.UpdatedAt = clock.GetUtcNow();
        if (commentId is null) {
            await Notice(p.AuthorId, actor, postId, "comment", "Có bình luận mới trên bài viết của bạn.", "comment:" + c.Id, ct);
            if (c.ParentId is not null) {
                var parentOwner = await db.CommunityComments.Where(x => x.Id == c.ParentId).Select(x => x.AuthorId).SingleAsync(ct);
                await Notice(parentOwner, actor, postId, "reply", "Có người trả lời bình luận của bạn.", "reply:" + c.Id, ct);
            }
        }
        await MentionNotices(actor, postId, c.Body, "comment:" + c.Id, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return c.Id;
    }
    public async Task DeleteComment(Guid actor, bool admin, Guid postId, Guid id, long version, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockUser(actor, ct); await LockPost(postId, ct);
        var c = await db.CommunityComments.SingleOrDefaultAsync(x => x.Id == id && x.PostId == postId, ct) ?? throw new CommunityError(404, "community_not_found");
        Ensure(c.AuthorId == actor || admin, 404, "community_not_found"); Ensure(c.RowVersion == version, 409, "community_conflict");
        c.IsDeleted = true; c.Body = ""; c.RowVersion++; c.UpdatedAt = clock.GetUtcNow();
        if (admin) db.AdminAudits.Add(new() { ActorId = actor, TargetId = c.Id, Action = "community_comment_remove", Reason = "Xóa bình luận qua kiểm duyệt cộng đồng" });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<Guid> Report(Guid actor, ReportCreate input, CancellationToken ct)
    {
        Ensure(input.Reason?.Trim().Length is >= 3 and <= 1000, 400, "invalid_community_report");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockUser(actor, ct); await LockPost(input.PostId, ct);
        Ensure(await Published().AnyAsync(x => x.Id == input.PostId, ct), 404, "community_not_found");
        if (input.CommentId is not null) Ensure(await db.CommunityComments.AnyAsync(x => x.Id == input.CommentId && x.PostId == input.PostId && !x.IsDeleted, ct), 404, "community_not_found");
        Ensure(await db.CommunityReports.CountAsync(x => x.ReporterId == actor && x.CreatedAt > clock.GetUtcNow().AddDays(-1), ct) < 20, 429, "community_report_limit");
        Ensure(!await db.CommunityReports.AnyAsync(x => x.ReporterId == actor && x.PostId == input.PostId && x.CommentId == input.CommentId && (x.Status == "open" || x.Status == "reviewed"), ct), 409, "community_report_exists");
        var report = new CommunityReport { ReporterId = actor, PostId = input.PostId, CommentId = input.CommentId, Reason = input.Reason!.Trim(), CreatedAt = clock.GetUtcNow() };
        db.CommunityReports.Add(report); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return report.Id;
    }
    public async Task ModerateReport(Guid admin, Guid id, ModerationAction input, CancellationToken ct)
    {
        Ensure(input.Reason?.Trim().Length is >= 3 and <= 1000 && input.Action is "review" or "resolve" or "dismiss" or "warn" or "hide", 400, "community_reason_required");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var report = await db.CommunityReports.FromSqlInterpolated($"SELECT * FROM community_reports WHERE id={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new CommunityError(404, "community_not_found");
        Ensure(report.RowVersion == input.RowVersion && report.Status is "open" or "reviewed", 409, "community_conflict");
        var post = await LockPost(report.PostId, ct);
        if(input.PostRowVersion is {} postVersion)Ensure(post.RowVersion==postVersion,409,"community_conflict");
        if(input.Action is "hide" or "warn")Ensure(post.Status!="draft"&&post.Status!="deleted"&&await db.Users.AnyAsync(u=>u.Id==post.AuthorId&&u.AccountStatus=="active",ct),409,"community_content_unavailable");
        var owner = post.AuthorId;
        if (report.CommentId is not null) {
            var c = await db.CommunityComments.SingleOrDefaultAsync(x => x.Id == report.CommentId && x.PostId == post.Id, ct) ?? throw new CommunityError(404, "community_not_found");
            owner = c.AuthorId;
            if(input.CommentRowVersion is {} commentVersion)Ensure(c.RowVersion==commentVersion,409,"community_conflict");
            if(input.Action is "hide" or "warn")Ensure(!c.IsDeleted&&await db.Users.AnyAsync(u=>u.Id==c.AuthorId&&u.AccountStatus=="active",ct),409,"community_content_unavailable");
            if (input.Action is "hide" or "warn") { c.IsDeleted = true; c.Body = ""; c.RowVersion++; c.UpdatedAt = clock.GetUtcNow(); }
        } else if (input.Action is "hide" or "warn") { post.Status = "hidden"; post.RowVersion++; post.UpdatedAt = clock.GetUtcNow(); }
        report.Status = input.Action switch { "review" => "reviewed", "dismiss" => "dismissed", _ => "resolved" };
        report.Resolution = input.Reason!.Trim(); report.ReviewedBy = admin; report.ReviewedAt = clock.GetUtcNow(); report.RowVersion++;
        db.AdminAudits.Add(new() { ActorId = admin, TargetId = report.Id, Action = "community_report_" + input.Action, Reason = report.Resolution });
        if (input.Action == "warn") await Notice(owner, admin, null, "warning", "Quản trị viên đã cảnh cáo và gỡ nội dung vi phạm: " + report.Resolution[..Math.Min(400, report.Resolution.Length)], "warning:" + report.Id, ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
