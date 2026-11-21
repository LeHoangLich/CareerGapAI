using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Recruitment;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class BusinessIdentityTests(FoundationFixture fixture):IClassFixture<FoundationFixture>
{
    private async Task<(HttpClient Client,Guid User,Guid Company)> Create()
    {
        var (client,uid)=await fixture.User("Recruiter");
        var response=await client.PostAsJsonAsync("/api/companies",new {name="Synthetic legal identity",websiteUrl="https://example.invalid",location="Hà Nội"});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return(client,uid,(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }
    private async Task Confirm(HttpClient client,Guid uid)
    {
        using var scope=fixture.App.Services.CreateScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();var user=(await users.FindByIdAsync(uid.ToString()))!;
        var token=await users.GenerateEmailConfirmationTokenAsync(user);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/recruiter/business/email/confirm",new {token="invalid"})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/recruiter/business/email/confirm",new {token})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/recruiter/business/email/confirm",new {token})).StatusCode);
    }
    private static async Task<JsonElement> Identity(HttpClient client)=>(await client.GetFromJsonAsync<JsonElement>("/api/recruiter/business")).GetProperty("identity");
    [Theory]
    [InlineData("hr@gmail.com",false)] [InlineData("hr@outlook.com",false)] [InlineData("hr@example.invalid",true)]
    public void CorporateEmailPolicy(string email,bool valid)=>Assert.Equal(valid,BusinessIdentityEndpoints.CorporateEmail(email));
    [Fact] public async Task EmailAndAdminApprovalGatePublishingAndChangesRequireReapprovalWithoutChangingLegalEntity()
    {
        var s=await Create();var (admin,_)=await fixture.User("Admin");var (candidate,_)=await fixture.User();
        var submit=new BusinessSubmission("Synthetic Legal Company","0987654321",0);
        Assert.Equal(HttpStatusCode.Conflict,(await s.Client.PutAsJsonAsync("/api/recruiter/business",submit)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await s.Client.PostAsync("/api/recruiter/business/email/request",null)).StatusCode);
        await Confirm(s.Client,s.User);
        Assert.Equal(HttpStatusCode.NoContent,(await s.Client.PutAsJsonAsync("/api/recruiter/business",submit)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await s.Client.PutAsJsonAsync("/api/recruiter/business",submit)).StatusCode);
        var content=new JobContentRequest("Verified identity test","Synthetic role","React",null,null,"Hà Nội","full_time","remote",null,null,null,null,null,null,"vi");
        var result=await s.Client.PostAsJsonAsync("/api/jobs",new JobCreateRequest(s.Company,null,content));Assert.Equal(HttpStatusCode.Created,result.StatusCode);
        var job=(await result.Content.ReadFromJsonAsync<JobDto>())!;
        Assert.Equal(HttpStatusCode.Conflict,(await s.Client.PostAsJsonAsync($"/api/jobs/{job.Id}/publish",new {job.RowVersion})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await candidate.PostAsJsonAsync($"/api/admin/businesses/{s.User}",new BusinessReview("verify","Synthetic fixture",1))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await admin.PostAsJsonAsync($"/api/admin/businesses/{s.User}",new BusinessReview("verify","",1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/admin/businesses/{s.User}",new BusinessReview("verify","Synthetic fixture",0))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PostAsJsonAsync($"/api/admin/businesses/{s.User}",new BusinessReview("verify","Synthetic fixture only",1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await s.Client.PostAsJsonAsync($"/api/jobs/{job.Id}/publish",new {job.RowVersion})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await fixture.App.CreateClient().GetAsync($"/api/jobs/{job.Id}")).StatusCode);
        var company=(await s.Client.GetFromJsonAsync<CompanyDto>($"/api/companies/{s.Company}"))!;
        Assert.Equal(HttpStatusCode.OK,(await s.Client.PutAsJsonAsync($"/api/companies/{s.Company}",new CompanyUpdateRequest(company.RowVersion,"Renamed company",null,"https://example.invalid","Hà Nội",null,null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await fixture.App.CreateClient().GetAsync($"/api/jobs/{job.Id}")).StatusCode);
        var identity=await Identity(s.Client);
        Assert.Equal(HttpStatusCode.Conflict,(await s.Client.PutAsJsonAsync("/api/recruiter/business",new BusinessSubmission("Different legal entity","1234567890",identity.GetProperty("rowVersion").GetInt64()))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await s.Client.PutAsJsonAsync("/api/recruiter/business",submit with{RowVersion=identity.GetProperty("rowVersion").GetInt64()})).StatusCode);
    }
    [Fact] public async Task RejectResubmitTaxUniquenessAndDomainOwnershipAreEnforced()
    {
        var s=await Create();await Confirm(s.Client,s.User);var (admin,_)=await fixture.User("Admin");
        Assert.Equal(HttpStatusCode.BadRequest,(await s.Client.PutAsJsonAsync("/api/recruiter/business",new BusinessSubmission("Synthetic legal","123",0))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await s.Client.PutAsJsonAsync("/api/recruiter/business",new BusinessSubmission("Synthetic legal","1234567890",0))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await admin.PostAsJsonAsync($"/api/admin/businesses/{s.User}",new BusinessReview("reject","Please correct legal name",1))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await s.Client.PutAsJsonAsync("/api/recruiter/business",new BusinessSubmission("Corrected synthetic legal","1234567890",2))).StatusCode);
        var other=await Create();await Confirm(other.Client,other.User);
        Assert.Equal(HttpStatusCode.Conflict,(await other.Client.PutAsJsonAsync("/api/recruiter/business",new BusinessSubmission("Duplicate tax","1234567890",0))).StatusCode);
        var company=(await other.Client.GetFromJsonAsync<CompanyDto>($"/api/companies/{other.Company}"))!;
        Assert.Equal(HttpStatusCode.OK,(await other.Client.PutAsJsonAsync($"/api/companies/{other.Company}",new CompanyUpdateRequest(company.RowVersion,company.Name,null,"https://unrelated.invalid","Hà Nội",null,null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await other.Client.PutAsJsonAsync("/api/recruiter/business",new BusinessSubmission("Wrong domain","1112223334",1))).StatusCode);
    }
    [Fact] public async Task LegacyCompanyChoiceIsOwnedPermanentAndPreservesAllRows()
    {
        var (client,uid)=await fixture.User("Recruiter");var other=await Create();Guid a,b;
        using(var scope=fixture.App.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AppDb>();var first=new Company{OwnerUserId=uid,Name="Legacy A",Slug="a-"+Guid.NewGuid()};var second=new Company{OwnerUserId=uid,Name="Legacy B",Slug="b-"+Guid.NewGuid()};db.Companies.AddRange(first,second);await db.SaveChangesAsync();a=first.Id;b=second.Id;
        }
        Assert.Equal(HttpStatusCode.NotFound,(await client.PostAsync($"/api/recruiter/business/primary/{other.Company}",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsync($"/api/recruiter/business/primary/{a}",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsync($"/api/recruiter/business/primary/{a}",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsync($"/api/recruiter/business/primary/{b}",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync($"/api/companies/{b}",new CompanyUpdateRequest(0,"Legacy B",null,null,"Hà Nội",null,null))).StatusCode);
        var companies=await client.GetFromJsonAsync<CompanyDto[]>("/api/companies/mine");Assert.Equal(2,companies!.Length);
    }
    [Fact] public async Task RecruiterRegistrationRejectsPersonalEmailWhileCandidateRegistrationAllowsIt()
    {
        var client=fixture.App.CreateClient();await FoundationFixture.Csrf(client);var email=$"synthetic-{Guid.NewGuid():N}@gmail.com";
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/auth/register",new {email,password="Synthetic!Password2026",role="Recruiter"})).StatusCode);
        Assert.Equal(HttpStatusCode.Created,(await client.PostAsJsonAsync("/api/auth/register",new {email,password="Synthetic!Password2026",role="Candidate"})).StatusCode);
    }
}
