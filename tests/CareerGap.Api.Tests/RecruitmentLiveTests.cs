using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class RecruitmentLiveTests(FoundationFixture fixture):IClassFixture<FoundationFixture>
{
 private static async Task<JsonElement> ReadSignal(StreamReader reader,CancellationToken ct){while(await reader.ReadLineAsync(ct) is {} line){if(line.StartsWith("data: "))return JsonDocument.Parse(line[6..]).RootElement.GetProperty("recruitment").Clone();}throw new InvalidOperationException("Stream ended before notification signal");}
 [Fact] public async Task StreamUpdatesOwnedUnreadReadAllAndReconnectWithoutSendingContent(){
  var (client,uid)=await fixture.User();var (other,oid)=await fixture.User();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));
  using var stream=await client.SendAsync(new(HttpMethod.Get,"/api/community/events"),HttpCompletionOption.ResponseHeadersRead,timeout.Token);Assert.Equal("text/event-stream",stream.Content.Headers.ContentType!.MediaType);using var reader=new StreamReader(await stream.Content.ReadAsStreamAsync(timeout.Token));var first=await ReadSignal(reader,timeout.Token);Assert.Equal(0,first.GetProperty("unread").GetInt32());var initial=first.GetProperty("revision").GetString();
  var notice=new NotificationRecord{RecipientUserId=uid,Title="PRIVATE_TITLE_F24",Body="PRIVATE_BODY_F24",DeduplicationKey=Guid.NewGuid().ToString()};
  using(var scope=fixture.App.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDb>();db.Notifications.AddRange(notice,new NotificationRecord{RecipientUserId=oid,Title="FOREIGN_F24",DeduplicationKey=Guid.NewGuid().ToString()});await db.SaveChangesAsync();}
  var next=await ReadSignal(reader,timeout.Token);Assert.Equal(1,next.GetProperty("total").GetInt32());Assert.Equal(1,next.GetProperty("unread").GetInt32());Assert.NotEqual(initial,next.GetProperty("revision").GetString());Assert.DoesNotContain("PRIVATE",next.GetRawText());Assert.DoesNotContain(notice.Id.ToString(),next.GetRawText());
  Assert.Equal(HttpStatusCode.NotFound,(await other.PutAsync($"/api/notifications/{notice.Id}/read",null)).StatusCode);Assert.Equal(HttpStatusCode.NoContent,(await client.PutAsync("/api/notifications/read-all",null)).StatusCode);var read=await ReadSignal(reader,timeout.Token);Assert.Equal(0,read.GetProperty("unread").GetInt32());Assert.Equal(1,read.GetProperty("total").GetInt32());
  using var otherStream=await other.SendAsync(new(HttpMethod.Get,"/api/community/events"),HttpCompletionOption.ResponseHeadersRead,timeout.Token);using var otherReader=new StreamReader(await otherStream.Content.ReadAsStreamAsync(timeout.Token));var foreign=await ReadSignal(otherReader,timeout.Token);Assert.Equal(1,foreign.GetProperty("unread").GetInt32());
  using var reconnect=await client.SendAsync(new(HttpMethod.Get,"/api/community/events"),HttpCompletionOption.ResponseHeadersRead,timeout.Token);using var reReader=new StreamReader(await reconnect.Content.ReadAsStreamAsync(timeout.Token));var again=await ReadSignal(reReader,timeout.Token);Assert.Equal(read.GetProperty("revision").GetString(),again.GetProperty("revision").GetString());
 }
 [Fact] public async Task DigestChangesWhenSameCountsHaveDifferentIdsAndUnauthenticatedAccessIsDenied(){
  var (client,uid)=await fixture.User();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));var created=DateTimeOffset.UtcNow;
  using(var scope=fixture.App.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDb>();db.Notifications.Add(new(){RecipientUserId=uid,Title="First",DeduplicationKey="one",CreatedAt=created});await db.SaveChangesAsync();}
  using var stream=await client.SendAsync(new(HttpMethod.Get,"/api/community/events"),HttpCompletionOption.ResponseHeadersRead,timeout.Token);using var reader=new StreamReader(await stream.Content.ReadAsStreamAsync(timeout.Token));var first=await ReadSignal(reader,timeout.Token);
  using(var scope=fixture.App.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDb>();await db.Notifications.Where(n=>n.RecipientUserId==uid).ExecuteDeleteAsync();db.Notifications.Add(new(){RecipientUserId=uid,Title="Replacement",DeduplicationKey="two",CreatedAt=created});await db.SaveChangesAsync();}
  var next=await ReadSignal(reader,timeout.Token);Assert.Equal(first.GetProperty("total").GetInt32(),next.GetProperty("total").GetInt32());Assert.NotEqual(first.GetProperty("revision").GetString(),next.GetProperty("revision").GetString());Assert.Equal(HttpStatusCode.Unauthorized,(await fixture.App.CreateClient().GetAsync("/api/community/events")).StatusCode);
 }
 [Fact] public async Task StreamUsesTheSameCurrentApplicationVisibilityAndRetentionAsInbox(){
  var (candidate,cid)=await fixture.User();var (owner,uid)=await fixture.User("Recruiter");var (admin,_)=await fixture.User("Admin");
  var cv=(await FoundationFixture.Upload(candidate)).GetProperty("versionId").GetGuid();var companyResponse=await owner.PostAsJsonAsync("/api/companies",new{name="F24 visibility fixture"});var company=(await companyResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  var jobResponse=await owner.PostAsJsonAsync("/api/jobs",new{companyId=company,content=new{title="F24 visibility job",description="Synthetic F24 visibility test",requirementsText="Testing",employmentType="full_time",workMode="remote",language="vi"}});var job=await jobResponse.Content.ReadFromJsonAsync<JsonElement>();var jid=job.GetProperty("id").GetGuid();Assert.Equal(HttpStatusCode.OK,(await fixture.PublishWithVerifiedBusinessFixture(owner,jid,new{rowVersion=job.GetProperty("rowVersion").GetInt64()})).StatusCode);
  var applied=await candidate.PostAsJsonAsync($"/api/jobs/{jid}/applications",new{resumeVersionId=cv,submissionKey=Guid.NewGuid(),coverLetter="PRIVATE_F24_COVER"});Assert.Equal(HttpStatusCode.Created,applied.StatusCode);var aid=(await applied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
  using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(25));using var response=await owner.SendAsync(new(HttpMethod.Get,"/api/community/events"),HttpCompletionOption.ResponseHeadersRead,timeout.Token);using var reader=new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));var first=await ReadSignal(reader,timeout.Token);Assert.Equal(1,first.GetProperty("total").GetInt32());
  await admin.PostAsJsonAsync($"/api/admin/accounts/{cid}",new AdminAction("suspend","F24 visibility revoke"));var hidden=await ReadSignal(reader,timeout.Token);Assert.Equal(0,hidden.GetProperty("total").GetInt32());
  await admin.PostAsJsonAsync($"/api/admin/accounts/{cid}",new AdminAction("activate","F24 visibility restore"));var restored=await ReadSignal(reader,timeout.Token);Assert.Equal(1,restored.GetProperty("total").GetInt32());
  using(var scope=fixture.App.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<AppDb>();var application=await db.Applications.SingleAsync(x=>x.Id==aid);application.Status="withdrawn";application.WithdrawnAt=DateTimeOffset.UtcNow;application.StatusChangedAt=DateTimeOffset.UtcNow.AddDays(-91);application.RowVersion++;await db.SaveChangesAsync();
  }
  var expired=await ReadSignal(reader,timeout.Token);Assert.Equal(0,expired.GetProperty("total").GetInt32());Assert.DoesNotContain("PRIVATE_F24_COVER",expired.GetRawText());
 }
 [Fact] public async Task RevokedSessionClosesStreamAndDoesNotDeliverLaterNotifications(){
  var (client,uid)=await fixture.User();var (admin,_)=await fixture.User("Admin");using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));using var response=await client.SendAsync(new(HttpMethod.Get,"/api/community/events"),HttpCompletionOption.ResponseHeadersRead,timeout.Token);using var reader=new StreamReader(await response.Content.ReadAsStreamAsync(timeout.Token));await ReadSignal(reader,timeout.Token);
  Assert.Equal(HttpStatusCode.NoContent,(await admin.PostAsJsonAsync($"/api/admin/accounts/{uid}",new AdminAction("suspend","F24 revoke synthetic session"))).StatusCode);
  while(await reader.ReadLineAsync(timeout.Token) is {} line)Assert.DoesNotContain("data: ",line);
  Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/notifications/inbox")).StatusCode);
 }
}
