using System.Net;
using System.Net.Http.Json;
using CareerGap.Api.Community;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class CommunityTagTests(FoundationFixture fixture):IClassFixture<FoundationFixture>
{
 private async Task<(HttpClient Client,Guid Id)> Member(){var (client,id)=await fixture.User();await client.PutAsJsonAsync("/api/community/me",new ProfileEdit("t"+id.ToString("N")[..15],"Multi tag fixture","Public bio",null,true,0));return(client,id);}
 [Fact] public async Task MultipleTagsUseUnionBeforePagingAndCombineWithLegacyAuthorCategoryAndKeyword(){
  var owner=await Member();var other=await Member();var prefix="f29-"+Guid.NewGuid();var expected=new HashSet<Guid>();
  using(var scope=fixture.App.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<AppDb>();foreach(var name in new[]{prefix,"react","docker","java","c#","c++",".net","kỹ năng mềm"})db.CommunityTags.Add(new(){Name=name});
   for(var i=0;i<58;i++){
    var post=new CommunityPost{AuthorId=i==57?other.Id:owner.Id,Title=prefix+" source "+i,Slug="tag-"+Guid.NewGuid(),Body="Synthetic multi tag source",Status=i==56?"draft":i==55?"pending":i==54?"hidden":"published",Category=i%2==0?"technical":"career",PublishedAt=DateTimeOffset.Parse("2026-11-01T00:00:00Z")};db.CommunityPosts.Add(post);
    var tags=i<53?new[]{prefix,"react","docker"}:i==53?new[]{prefix,"react"}:new[]{prefix,"react","docker","java"};foreach(var tag in tags)db.CommunityPostTags.Add(new(){PostId=post.Id,Tag=tag});if(post.Status=="published")expected.Add(post.Id);
   }
   var special=new CommunityPost{AuthorId=owner.Id,Title=prefix+" special",Slug="tag-"+Guid.NewGuid(),Body="Special literal terms",Status="published"};db.CommunityPosts.Add(special);foreach(var tag in new[]{prefix,"c#","c++",".net","kỹ năng mềm"})db.CommunityPostTags.Add(new(){PostId=special.Id,Tag=tag});await db.SaveChangesAsync();
  }
  var guest=fixture.App.CreateClient();var all=new HashSet<Guid>();for(var page=1;page<=5;page++){var r=(await guest.GetFromJsonAsync<SocialPage<SocialPost>>($"/api/community/posts?q={prefix}&tag=%20REACT%20&tags=docker,react&page={page}"))!;Assert.Equal(expected.Count,r.Total);foreach(var p in r.Items)Assert.True(all.Add(p.Id));}Assert.True(expected.SetEquals(all));
  var combined=(await guest.GetFromJsonAsync<SocialPage<SocialPost>>($"/api/community/posts?tags=react,docker&category=technical&q={prefix}&author={owner.Id}"))!;Assert.All(combined.Items,p=>{Assert.Equal(owner.Id,p.AuthorId);Assert.Equal("technical",p.Category);});Assert.Equal(27,combined.Total);
  var literal=(await guest.GetFromJsonAsync<SocialPage<SocialPost>>($"/api/community/posts?q={prefix}&tags="+Uri.EscapeDataString("c#,c++,.net,kỹ năng mềm")))!;Assert.Equal(1,literal.Total);
  Assert.Equal(0,(await guest.GetFromJsonAsync<SocialPage<SocialPost>>($"/api/community/posts?q={prefix}&tags=unknown-valid"))!.Total);
  foreach(var tags in new[]{"a,b,c,d,e,f,g,h,i","react,,docker","react,invalid$",new string('x',41)})Assert.Equal(HttpStatusCode.BadRequest,(await guest.GetAsync("/api/community/posts?tags="+Uri.EscapeDataString(tags))).StatusCode);
  Assert.Equal(HttpStatusCode.BadRequest,(await guest.GetAsync("/api/community/posts?tag=legacy&tags=a,b,c,d,e,f,g,h")).StatusCode);
 }
 [Fact] public async Task MatchCountRanksBeforeAllSortOptionsAndNoFilterKeepsOriginalSort(){
  var owner=await Member();var viewer=await Member();var prefix="rank-"+Guid.NewGuid().ToString("N")[..12];
  using(var scope=fixture.App.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<AppDb>();db.CommunityTags.AddRange(new CommunityTag{Name=prefix+"-java"},new CommunityTag{Name=prefix+"-react"});
   var now=DateTimeOffset.UtcNow;var full=new CommunityPost{AuthorId=owner.Id,Title=prefix+" both",Slug=Guid.NewGuid().ToString(),Status="published",PublishedAt=now.AddDays(-4)};
   var react=new CommunityPost{AuthorId=owner.Id,Title=prefix+" react",Slug=Guid.NewGuid().ToString(),Status="published",PublishedAt=now.AddDays(-2)};
   var java=new CommunityPost{AuthorId=owner.Id,Title=prefix+" java",Slug=Guid.NewGuid().ToString(),Status="published",PublishedAt=now.AddDays(-3)};
   var unrelated=new CommunityPost{AuthorId=owner.Id,Title=prefix+" unrelated",Slug=Guid.NewGuid().ToString(),Status="published",PublishedAt=now.AddDays(-1)};db.CommunityPosts.AddRange(full,react,java,unrelated);
   foreach(var (post,tag)in new[]{(full,prefix+"-java"),(full,prefix+"-react"),(react,prefix+"-react"),(java,prefix+"-java")})db.CommunityPostTags.Add(new(){PostId=post.Id,Tag=tag});
   db.CommunityViews.AddRange(new CommunityView{PostId=react.Id,Viewer="one",Day=DateOnly.FromDateTime(DateTime.UtcNow)},new CommunityView{PostId=java.Id,Viewer="one",Day=DateOnly.FromDateTime(DateTime.UtcNow)},new CommunityView{PostId=react.Id,Viewer="two",Day=DateOnly.FromDateTime(DateTime.UtcNow)});db.CommunityReactions.Add(new(){PostId=react.Id,UserId=viewer.Id});await db.SaveChangesAsync();
  }
  var guest=fixture.App.CreateClient();foreach(var sort in new[]{"newest","views","trending"}){
   var result=(await guest.GetFromJsonAsync<SocialPage<SocialPost>>($"/api/community/posts?q={prefix}&tags={prefix}-java,{prefix}-react&sort={sort}"))!;Assert.Equal(3,result.Total);Assert.Equal(new[]{prefix+" both",prefix+" react",prefix+" java"},result.Items.Select(p=>p.Title));
  }
  var unfiltered=(await guest.GetFromJsonAsync<SocialPage<SocialPost>>($"/api/community/posts?q={prefix}&sort=newest"))!;Assert.Equal(4,unfiltered.Total);Assert.Equal(prefix+" unrelated",unfiltered.Items[0].Title);
 }
 [Fact] public async Task PersonalFeedsKeepOwnershipVisibilityAndAuthenticationWithTagUnion(){
  var owner=await Member();var other=await Member();var tag="scope-"+Guid.NewGuid().ToString("N");Guid draftId,publishedId;
  using(var scope=fixture.App.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<AppDb>();db.CommunityTags.AddRange(new CommunityTag{Name=tag},new CommunityTag{Name="scope-react"});
   var draft=new CommunityPost{AuthorId=owner.Id,Title="Private tagged draft",Body="PRIVATE TAGGED CONTENT",Status="draft",Slug="scope-"+Guid.NewGuid()};var published=new CommunityPost{AuthorId=other.Id,Title="Other published source",Body="Public source",Status="published",Slug="scope-"+Guid.NewGuid()};db.CommunityPosts.AddRange(draft,published);foreach(var p in new[]{draft,published})foreach(var t in new[]{tag,"scope-react"})db.CommunityPostTags.Add(new(){PostId=p.Id,Tag=t});await db.SaveChangesAsync();draftId=draft.Id;publishedId=published.Id;
   db.CommunityBookmarks.Add(new(){PostId=published.Id,UserId=owner.Id});db.CommunityFollows.Add(new(){FollowerId=owner.Id,FollowingId=other.Id});await db.SaveChangesAsync();
  }
  var query="&tags="+tag+",scope-react";
  var mine=(await owner.Client.GetFromJsonAsync<SocialPage<SocialPost>>("/api/community/posts?mode=mine"+query))!;Assert.Equal(draftId,Assert.Single(mine.Items).Id);
  var saved=(await owner.Client.GetFromJsonAsync<SocialPage<SocialPost>>("/api/community/posts?mode=saved"+query))!;Assert.Equal(publishedId,Assert.Single(saved.Items).Id);
  var following=(await owner.Client.GetFromJsonAsync<SocialPage<SocialPost>>("/api/community/posts?mode=following"+query))!;Assert.Equal(publishedId,Assert.Single(following.Items).Id);
  var guest=fixture.App.CreateClient();foreach(var mode in new[]{"mine","saved","following"})Assert.Equal(HttpStatusCode.Unauthorized,(await guest.GetAsync("/api/community/posts?mode="+mode+query)).StatusCode);
  using(var scope=fixture.App.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDb>();await db.Users.Where(x=>x.Id==other.Id).ExecuteUpdateAsync(x=>x.SetProperty(u=>u.AccountStatus,"suspended"));}
  Assert.Empty((await owner.Client.GetFromJsonAsync<SocialPage<SocialPost>>("/api/community/posts?mode=saved"+query))!.Items);Assert.Empty((await guest.GetFromJsonAsync<SocialPage<SocialPost>>("/api/community/posts?mode=discover"+query))!.Items);
 }
}
