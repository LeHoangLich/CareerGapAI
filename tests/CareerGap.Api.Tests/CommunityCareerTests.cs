using System.Net;
using System.Net.Http.Json;
using CareerGap.Api.Community;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class CommunityCareerTests(FoundationFixture fixture):IClassFixture<FoundationFixture>
{
    private async Task<(HttpClient Client,Guid Id,SocialProfile Profile)> Member(string role="Candidate",bool? show=null)
    {
        var (client,id)=await fixture.User(role);var response=await client.PutAsJsonAsync("/api/community/me",new ProfileEdit("c"+id.ToString("N")[..14],"Career fixture","Public bio",null,true,0,show));
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);return(client,id,(await response.Content.ReadFromJsonAsync<SocialProfile>())!);
    }
    private static async Task<SocialProfile> Read(HttpClient client,Guid id)=>(await client.GetFromJsonAsync<SocialProfile>($"/api/community/profiles/{id}"))!;
    [Fact] public async Task CandidateRequiresPublicConsentAndOnlyWhitelistedCareerFieldsLeaveTheServer()
    {
        var s=await Member();var guest=fixture.App.CreateClient();using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        var p=await db.CandidateProfiles.SingleAsync(x=>x.UserId==s.Id);p.Headline="React Developer";p.DesiredRole="Frontend Engineer";p.Location="Hà Nội";p.ExperienceYears=0;p.ContactEmail="private-career@example.invalid";p.ContactPhone="0900000000";p.Summary="PRIVATE SUMMARY";p.Details="""{"skills":["React","Docker","React"],"projects":["PRIVATE PROJECT"],"experience":["PRIVATE EMPLOYER"]}""";await db.SaveChangesAsync();
        var hidden=await Read(guest,s.Id);Assert.False(hidden.ShowCareer);Assert.Equal("Candidate",hidden.Career!.Role);Assert.Null(hidden.Career.Headline);Assert.Empty(hidden.Career.Skills);
        var request=new ProfileEdit(s.Profile.Handle,s.Profile.DisplayName,s.Profile.Bio,null,true,s.Profile.RowVersion,true);
        Assert.Equal(HttpStatusCode.OK,(await s.Client.PutAsJsonAsync("/api/community/me",request)).StatusCode);
        var shown=await Read(guest,s.Id);Assert.True(shown.ShowCareer);Assert.Equal("React Developer",shown.Career!.Headline);Assert.Equal(0,shown.Career.ExperienceYears);Assert.Equal(new[]{"React","Docker"},shown.Career.Skills);Assert.Null(shown.Career.Company);
        var body=await guest.GetStringAsync($"/api/community/profiles/{s.Id}");foreach(var secret in new[]{p.ContactEmail,p.ContactPhone,p.Summary,"PRIVATE PROJECT","PRIVATE EMPLOYER","contactEmail","sharedFields","resumeVersionId","fileId"})Assert.DoesNotContain(secret!,body);
        // Existing callers omitting the new preference preserve the current consent.
        Assert.Equal(HttpStatusCode.OK,(await s.Client.PutAsJsonAsync("/api/community/me",request with{RowVersion=shown.RowVersion,ShowCareer=null})).StatusCode);shown=await Read(guest,s.Id);Assert.True(shown.ShowCareer);
        p.Headline="Updated source headline";await db.SaveChangesAsync();Assert.Equal(p.Headline,(await Read(guest,s.Id)).Career!.Headline);
        Assert.Equal(HttpStatusCode.OK,(await s.Client.PutAsJsonAsync("/api/community/me",request with{RowVersion=shown.RowVersion,ShowCareer=false})).StatusCode);
        Assert.Null((await Read(guest,s.Id)).Career!.Headline);Assert.Equal("private-career@example.invalid",p.ContactEmail);
        Assert.Equal(HttpStatusCode.Conflict,(await s.Client.PutAsJsonAsync("/api/community/me",request)).StatusCode);
    }
    [Fact] public async Task RecruiterOnlyShowsVerifiedPrimaryCompanyAndNeverLegalOrAccountSecrets()
    {
        var s=await Member("Recruiter",true);var guest=fixture.App.CreateClient();using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        var company=new Company{OwnerUserId=s.Id,Name="Verified main company",Slug="main-"+s.Id,WebsiteUrl="https://example.invalid",Description="Public company description",SizeLabel="11-50",Industry="IT",Location="Hà Nội"};var legacy=new Company{OwnerUserId=s.Id,Name="PRIVATE LEGACY COMPANY",Slug="legacy-"+s.Id};db.Companies.AddRange(company,legacy);await db.SaveChangesAsync();
        var identity=new CareerGap.Api.Recruitment.BusinessIdentity{UserId=s.Id,CompanyId=company.Id,Status="pending",TaxCode="1234567890",LegalName="PRIVATE LEGAL NAME",ReviewReason="PRIVATE REVIEW REASON"};db.BusinessIdentities.Add(identity);await db.SaveChangesAsync();
        Assert.Null((await Read(guest,s.Id)).Career!.Company);identity.Status="verified";await db.SaveChangesAsync();var shown=await Read(guest,s.Id);Assert.Equal("Recruiter",shown.Career!.Role);Assert.Equal(company.Id,shown.Career.Company!.Id);Assert.Equal(company.WebsiteUrl,shown.Career.Company.WebsiteUrl);
        var body=await guest.GetStringAsync($"/api/community/profiles/{s.Id}");foreach(var secret in new[]{"PRIVATE LEGACY COMPANY","PRIVATE LEGAL NAME","PRIVATE REVIEW REASON","1234567890","taxCode","emailConfirmed"})Assert.DoesNotContain(secret,body);
        identity.Status="draft";await db.SaveChangesAsync();Assert.Null((await Read(guest,s.Id)).Career!.Company);
        identity.Status="verified";company.ArchivedAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();Assert.Null((await Read(guest,s.Id)).Career!.Company);
    }
    [Fact] public async Task RolesAreReadFromIdentityAndSuspendedOrDeletedAuthorsDisappear()
    {
        var s=await Member("Admin",true);var guest=fixture.App.CreateClient();Assert.Equal("Admin",(await Read(guest,s.Id)).Career!.Role);
        Assert.Equal(HttpStatusCode.OK,(await s.Client.PutAsJsonAsync("/api/community/me",new {s.Profile.Handle,s.Profile.DisplayName,s.Profile.Bio,allowMentions=true,rowVersion=s.Profile.RowVersion,showCareer=true,role="Recruiter",career=new{role="Recruiter"}})).StatusCode);Assert.Equal("Admin",(await Read(guest,s.Id)).Career!.Role);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();var account=await db.Users.SingleAsync(x=>x.Id==s.Id);account.AccountStatus="suspended";await db.SaveChangesAsync();Assert.Equal(HttpStatusCode.NotFound,(await guest.GetAsync($"/api/community/profiles/{s.Id}")).StatusCode);
        account.AccountStatus="deleted";await db.SaveChangesAsync();Assert.Equal(HttpStatusCode.NotFound,(await guest.GetAsync($"/api/community/profiles/{s.Id}")).StatusCode);
    }
    [Fact] public async Task EmptyOrLegacyCandidateDetailsDoNotInventExperienceOrBreakCard()
    {
        var s=await Member(show:true);using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();var p=await db.CandidateProfiles.SingleAsync(x=>x.UserId==s.Id);p.Details="[]";await db.SaveChangesAsync();var shown=await Read(fixture.App.CreateClient(),s.Id);Assert.Null(shown.Career!.ExperienceYears);Assert.Empty(shown.Career.Skills);
    }
}
