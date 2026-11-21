using System.Net;
using System.Net.Http.Json;
using CareerGap.Api.Community;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class CommunityReportTests(FoundationFixture fixture):IClassFixture<FoundationFixture>
{
 const string Root="/api/community/admin/reports";
 private async Task<(HttpClient Client,Guid Id)> Member(string role="Candidate"){
  var (client,id)=await fixture.User(role);Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync("/api/community/me",new ProfileEdit("r"+id.ToString("N")[..15],"Report member "+role,"Public bio",null,true,0))).StatusCode);return(client,id);
 }
 private async Task<(CommunityPost Post,CommunityComment Comment,CommunityReport Report,Guid Author,Guid CommentAuthor,Guid Reporter)> Seed(){
  var a=await Member();var b=await Member();var c=await Member();using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
  var post=new CommunityPost{AuthorId=a.Id,Title="Report context title",Summary="Public context summary",Body="Public post body for report review.",Status="published",Slug="f30-"+Guid.NewGuid()};db.CommunityPosts.Add(post);
  var comment=new CommunityComment{PostId=post.Id,AuthorId=b.Id,Body="Reported comment contents."};db.CommunityComments.Add(comment);
  var report=new CommunityReport{PostId=post.Id,CommentId=comment.Id,ReporterId=c.Id,Reason="F30 synthetic comment report"};db.CommunityReports.Add(report);await db.SaveChangesAsync();return(post,comment,report,a.Id,b.Id,c.Id);
 }
 [Fact] public async Task FiltersCountBeforePagingAndReachAllRowsWithStableTies(){
  var owner=await Member();var reporter=await Member();var admin=await Member("Admin");var prefix="paging-"+Guid.NewGuid();
  using(var scope=fixture.App.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<AppDb>();for(var i=0;i<53;i++){
    var p=new CommunityPost{AuthorId=owner.Id,Title=prefix+" post "+i,Slug="paging-"+Guid.NewGuid(),Body="Public fixture content",Status="published"};db.CommunityPosts.Add(p);CommunityComment? c=null;if(i%2==1){c=new(){PostId=p.Id,AuthorId=owner.Id,Body="Comment "+prefix};db.CommunityComments.Add(c);}db.CommunityReports.Add(new(){PostId=p.Id,CommentId=c?.Id,ReporterId=reporter.Id,Reason=prefix,Status=new[]{"open","reviewed","resolved","dismissed"}[i%4],CreatedAt=DateTimeOffset.Parse("2026-11-01T00:00:00Z")});}await db.SaveChangesAsync();
  }
  var ids=new HashSet<Guid>();for(var page=1;page<=3;page++){var result=(await admin.Client.GetFromJsonAsync<SocialPage<CommunityReportItem>>(Root+"?q="+prefix+"&page="+page))!;Assert.Equal(53,result.Total);Assert.Equal(20,result.PageSize);foreach(var r in result.Items)Assert.True(ids.Add(r.Id));}Assert.Equal(53,ids.Count);
  foreach(var status in new[]{"open","reviewed","resolved","dismissed"})foreach(var type in new[]{"post","comment"}){
   var result=(await admin.Client.GetFromJsonAsync<SocialPage<CommunityReportItem>>(Root+$"?q={prefix}&status={status}&targetType={type}"))!;var expected=Enumerable.Range(0,53).Count(i=>new[]{"open","reviewed","resolved","dismissed"}[i%4]==status&&(i%2==0)==(type=="post"));Assert.Equal(expected,result.Total);Assert.All(result.Items,r=>{Assert.Equal(status,r.Status);Assert.Equal(type=="comment",r.CommentId!=null);});
  }
  foreach(var query in new[]{"page=0","page=10001","status=bad","targetType=job","q="+new string('a',121)})Assert.Equal(HttpStatusCode.BadRequest,(await admin.Client.GetAsync(Root+"?"+query)).StatusCode);
 }
 [Fact] public async Task CommentDetailHasCorrectActorsAndWarningChecksBothVersionsAndRecordsHistory(){
  var s=await Seed();var admin=await Member("Admin");var detail=(await admin.Client.GetFromJsonAsync<CommunityReportDetail>(Root+"/"+s.Report.Id))!;
  Assert.Equal(s.Reporter,detail.Reporter!.UserId);Assert.Equal(s.CommentAuthor,detail.Target!.Author!.UserId);Assert.Equal(s.Post.Body,detail.Target.Body);Assert.Equal(s.Comment.Body,detail.Target.CommentBody);Assert.Empty(detail.History);
  using(var scope=fixture.App.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDb>();var c=await db.CommunityComments.SingleAsync(x=>x.Id==s.Comment.Id);c.RowVersion++;c.Body="Edited comment contents";await db.SaveChangesAsync();}
  Assert.Equal(HttpStatusCode.Conflict,(await admin.Client.PostAsJsonAsync(Root+"/"+s.Report.Id,new ModerationAction("warn","Synthetic reason",0,0,0))).StatusCode);
  Assert.Equal(HttpStatusCode.NoContent,(await admin.Client.PostAsJsonAsync(Root+"/"+s.Report.Id,new ModerationAction("warn","Synthetic warning reason",0,0,1))).StatusCode);
  detail=(await admin.Client.GetFromJsonAsync<CommunityReportDetail>(Root+"/"+s.Report.Id))!;Assert.Equal("resolved",detail.Report.Status);Assert.Null(detail.Target!.CommentBody);Assert.False(detail.Target.CanRemove);Assert.Equal("published",detail.Target.PostStatus);Assert.Equal(admin.Id,detail.Reviewer!.UserId);Assert.Single(detail.History);Assert.Equal("community_report_warn",detail.History[0].Action);
  Assert.Equal(HttpStatusCode.Conflict,(await admin.Client.PostAsJsonAsync(Root+"/"+s.Report.Id,new ModerationAction("dismiss","Cannot overwrite",1))).StatusCode);
  using var final=fixture.App.Services.CreateScope();var db2=final.ServiceProvider.GetRequiredService<AppDb>();Assert.True(await db2.CommunityNotices.AnyAsync(x=>x.RecipientId==s.CommentAuthor&&x.Kind=="warning"));Assert.False(await db2.CommunityNotices.AnyAsync(x=>x.RecipientId==s.Author&&x.Kind=="warning"));
 }
 [Fact] public async Task PrivateDraftIsRedactedInListsDetailsAndSearchAndCannotBeRemovedViaOldReport(){
  var s=await Seed();var admin=await Member("Admin");const string secret="PRIVATE_F30_NEW_DRAFT";
  using(var scope=fixture.App.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDb>();var p=await db.CommunityPosts.SingleAsync(x=>x.Id==s.Post.Id);p.Status="draft";p.Title=secret;p.Summary=secret;p.Body=secret;p.RowVersion++;var c=await db.CommunityComments.SingleAsync(x=>x.Id==s.Comment.Id);c.Body=secret;await db.SaveChangesAsync();}
  Assert.DoesNotContain(secret,await admin.Client.GetStringAsync(Root+"/"+s.Report.Id));Assert.DoesNotContain(secret,await admin.Client.GetStringAsync(Root));var query=(await admin.Client.GetFromJsonAsync<SocialPage<CommunityReportItem>>(Root+"?q="+secret))!;Assert.Equal(0,query.Total);
  var detail=(await admin.Client.GetFromJsonAsync<CommunityReportDetail>(Root+"/"+s.Report.Id))!;Assert.False(detail.Target!.Available);Assert.False(detail.Target.CanRemove);Assert.Null(detail.Target.Title);Assert.Null(detail.Target.Body);Assert.Null(detail.Target.CommentBody);
  Assert.Equal(HttpStatusCode.Conflict,(await admin.Client.PostAsJsonAsync(Root+"/"+s.Report.Id,new ModerationAction("hide","Do not change private draft",0,1,0))).StatusCode);
  using(var scope=fixture.App.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDb>();var p=await db.CommunityPosts.SingleAsync(x=>x.Id==s.Post.Id);p.Status="deleted";p.PreviousStatus="draft";p.RowVersion++;await db.SaveChangesAsync();}
  Assert.DoesNotContain(secret,await admin.Client.GetStringAsync(Root+"/"+s.Report.Id));
  var deletedQuery=(await admin.Client.GetFromJsonAsync<SocialPage<CommunityReportItem>>(Root+"?q="+secret))!;Assert.Equal(0,deletedQuery.Total);
  Assert.Equal(HttpStatusCode.NoContent,(await admin.Client.PostAsJsonAsync(Root+"/"+s.Report.Id,new ModerationAction("dismiss","Content no longer public",0,2,0))).StatusCode);
 }
 [Fact] public async Task PostVersionGuardAndReviewResolveHistoryPreventStaleDecisions(){
  var s=await Seed();var admin=await Member("Admin");
  using(var scope=fixture.App.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDb>();var p=await db.CommunityPosts.SingleAsync(x=>x.Id==s.Post.Id);p.RowVersion++;p.Body="New public revision";await db.SaveChangesAsync();}
  Assert.Equal(HttpStatusCode.Conflict,(await admin.Client.PostAsJsonAsync(Root+"/"+s.Report.Id,new ModerationAction("hide","Stale post version",0,0,0))).StatusCode);
  Assert.Equal(HttpStatusCode.NoContent,(await admin.Client.PostAsJsonAsync(Root+"/"+s.Report.Id,new ModerationAction("review","Checking current context",0,1,0))).StatusCode);
  Assert.Equal(HttpStatusCode.NoContent,(await admin.Client.PostAsJsonAsync(Root+"/"+s.Report.Id,new ModerationAction("resolve","Checked and finished",1,1,0))).StatusCode);
  var detail=(await admin.Client.GetFromJsonAsync<CommunityReportDetail>(Root+"/"+s.Report.Id))!;Assert.Equal(2,detail.History.Length);Assert.Equal("resolved",detail.Report.Status);Assert.Equal(2,detail.Report.RowVersion);Assert.Equal("Checked and finished",detail.Report.Resolution);
 }
 [Fact] public async Task InboxAndDetailsAreAdminOnlyAndDoNotReturnAccountContacts(){
  var s=await Seed();var admin=await Member("Admin");var guest=fixture.App.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await guest.GetAsync(Root)).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await guest.GetAsync(Root+"/"+s.Report.Id)).StatusCode);
  foreach(var role in new[]{"Candidate","Recruiter"}){var member=await Member(role);Assert.Equal(HttpStatusCode.Forbidden,(await member.Client.GetAsync(Root)).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await member.Client.GetAsync(Root+"/"+s.Report.Id)).StatusCode);}
  Assert.Equal(HttpStatusCode.NotFound,(await admin.Client.GetAsync(Root+"/"+Guid.NewGuid())).StatusCode);var json=await admin.Client.GetStringAsync(Root+"/"+s.Report.Id);Assert.DoesNotContain("@example.invalid",json);Assert.DoesNotContain("password",json,StringComparison.OrdinalIgnoreCase);
  using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();var user=await db.Users.SingleAsync(x=>x.Id==s.Author);user.AccountStatus="suspended";await db.SaveChangesAsync();var detail=(await admin.Client.GetFromJsonAsync<CommunityReportDetail>(Root+"/"+s.Report.Id))!;Assert.Null(detail.Target!.Body);Assert.False(detail.Target.Available);
 }
}
