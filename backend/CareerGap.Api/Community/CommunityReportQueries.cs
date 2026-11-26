using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace CareerGap.Api.Community;
public sealed record CommunityReportItem(Guid Id,Guid PostId,Guid? CommentId,string Reason,string Status,string Resolution,Guid? ReviewedBy,long RowVersion,Guid ReportedAuthorId,string Content,DateTimeOffset CreatedAt,DateTimeOffset? ReviewedAt);
public sealed record ReportPerson(Guid UserId,string Name,string Handle,string Status,bool Protected);
public sealed record ReportedCommunityContent(Guid PostId,Guid? CommentId,string PostStatus,long PostRowVersion,long? CommentRowVersion,string? Title,string? Summary,string? Body,string? CommentBody,bool Available,bool CanRemove,ReportPerson? Author);
public sealed record CommunityReportAudit(Guid Id,string Action,string Reason,DateTimeOffset CreatedAt,Guid ActorId,string Actor);
public sealed record CommunityReportDetail(CommunityReportItem Report,ReportPerson? Reporter,ReportPerson? Reviewer,ReportedCommunityContent? Target,CommunityReportAudit[] History);
public static class CommunityReportQueries
{
    private static IQueryable<CommunityPost> VisiblePosts(AppDb db)=>db.CommunityPosts.Where(p=>p.Status!="draft"&&!(p.Status=="deleted"&&p.PreviousStatus=="draft")&&db.Users.Any(u=>u.Id==p.AuthorId&&u.AccountStatus=="active"));
    private static IQueryable<CommunityReportItem> Items(AppDb db,IQueryable<CommunityReport> q){var visible=VisiblePosts(db);return q.Select(r=>new CommunityReportItem(r.Id,r.PostId,r.CommentId,r.Reason,r.Status,r.Resolution,r.ReviewedBy,r.RowVersion,
        r.CommentId==null?db.CommunityPosts.Where(p=>p.Id==r.PostId).Select(p=>p.AuthorId).FirstOrDefault():db.CommunityComments.Where(c=>c.Id==r.CommentId).Select(c=>c.AuthorId).FirstOrDefault(),
        !visible.Any(p=>p.Id==r.PostId)?"Nội dung không khả dụng hoặc đã chuyển về nháp riêng tư.":r.CommentId==null?db.CommunityPosts.Where(p=>p.Id==r.PostId).Select(p=>p.Title).FirstOrDefault()??"Bài không còn khả dụng":db.CommunityComments.Where(c=>c.Id==r.CommentId&&!c.IsDeleted&&db.Users.Any(u=>u.Id==c.AuthorId&&u.AccountStatus=="active")).Select(c=>c.Body.Length>300?c.Body.Substring(0,300):c.Body).FirstOrDefault()??"Bình luận đã được gỡ hoặc không còn khả dụng",r.CreatedAt,r.ReviewedAt));}
    public static async Task<IResult> List(string? status,string? targetType,string? q,int? page,AppDb db,CancellationToken ct)
    {
        if(page is <1 or >10000||status is not(null or "all" or "open" or "reviewed" or "resolved" or "dismissed")||targetType is not(null or "all" or "post" or "comment")||(q?.Length??0)>120)return Results.Problem(statusCode:400,title:"invalid_community_report_query");
        var query=db.CommunityReports.AsNoTracking();if(status is not(null or "all"))query=query.Where(r=>r.Status==status);
        if(targetType=="post")query=query.Where(r=>r.CommentId==null);if(targetType=="comment")query=query.Where(r=>r.CommentId!=null);
        var term=q?.Trim().ToLowerInvariant();
        if(!string.IsNullOrEmpty(term)){
            Guid.TryParse(term,out var exactId);var visible=VisiblePosts(db);
            query=query.Where(r=>r.Id==exactId||r.PostId==exactId||r.Reason.ToLower().Contains(term)||r.Resolution.ToLower().Contains(term)||visible.Any(p=>p.Id==r.PostId&&(p.Title.ToLower().Contains(term)||(r.CommentId!=null&&db.CommunityComments.Any(c=>c.Id==r.CommentId&&!c.IsDeleted&&db.Users.Any(u=>u.Id==c.AuthorId&&u.AccountStatus=="active")&&c.Body.ToLower().Contains(term))))));
        }
        var total=await query.CountAsync(ct);var current=page??1;
        var rows=await Items(db,query.OrderByDescending(r=>r.CreatedAt).ThenBy(r=>r.Id).Skip((current-1)*20).Take(20)).ToArrayAsync(ct);
        return Results.Ok(new SocialPage<CommunityReportItem>(rows,current,20,total));
    }
    private static Task<ReportPerson?> Person(AppDb db,Guid id,CancellationToken ct)=>db.Users.AsNoTracking().Where(u=>u.Id==id).Select(u=>new ReportPerson(u.Id,
        db.CommunityProfiles.Where(p=>p.UserId==id&&u.AccountStatus!="deleted").Select(p=>p.DisplayName).FirstOrDefault()??"Thành viên không còn hồ sơ cộng đồng",
        db.CommunityProfiles.Where(p=>p.UserId==id&&u.AccountStatus!="deleted").Select(p=>p.Handle).FirstOrDefault()??"",u.AccountStatus,
        db.UserRoles.Any(ur=>ur.UserId==id&&db.Roles.Any(r=>r.Id==ur.RoleId&&r.Name=="Admin")))).SingleOrDefaultAsync(ct);
    public static async Task<IResult> Detail(Guid id,AppDb db,CancellationToken ct)
    {
        var report=await Items(db,db.CommunityReports.AsNoTracking().Where(r=>r.Id==id)).SingleOrDefaultAsync(ct);if(report is null)return Results.NotFound();
        var reporterId=await db.CommunityReports.Where(r=>r.Id==id).Select(r=>r.ReporterId).SingleAsync(ct);
        var post=await db.CommunityPosts.AsNoTracking().SingleOrDefaultAsync(p=>p.Id==report.PostId,ct);
        ReportedCommunityContent? target=null;
        if(post is not null){
            var comment=report.CommentId is {} cid?await db.CommunityComments.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==cid&&c.PostId==post.Id,ct):null;
            var visible=post.Status!="draft"&&await VisiblePosts(db).AnyAsync(p=>p.Id==post.Id&&p.RowVersion==post.RowVersion,ct);
            var commentVisible=comment is not null&&!comment.IsDeleted&&await db.Users.AnyAsync(u=>u.Id==comment.AuthorId&&u.AccountStatus=="active",ct);
            var available=visible&&(report.CommentId==null||commentVisible);
            target=new(post.Id,report.CommentId,post.Status,post.RowVersion,comment?.RowVersion,visible?post.Title:null,visible?post.Summary:null,visible?post.Body:null,
                visible&&commentVisible?comment!.Body:null,available,available&&post.Status!="deleted"&&(report.CommentId!=null||post.Status is "published" or "pending" or "hidden"),await Person(db,report.ReportedAuthorId,ct));
        }
        var history=await db.AdminAudits.AsNoTracking().Where(a=>a.TargetId==id&&a.Action.StartsWith("community_report_")).OrderBy(a=>a.CreatedAt).ThenBy(a=>a.Id)
            .Select(a=>new CommunityReportAudit(a.Id,a.Action,a.Reason,a.CreatedAt,a.ActorId,db.CommunityProfiles.Where(p=>p.UserId==a.ActorId).Select(p=>p.DisplayName).FirstOrDefault()??"Quản trị viên")).ToArrayAsync(ct);
        return Results.Ok(new CommunityReportDetail(report,await Person(db,reporterId,ct),report.ReviewedBy is {} reviewer?await Person(db,reviewer,ct):null,target,history));
    }
}
