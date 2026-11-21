using System.Net;
using System.Net.Http.Json;
using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed class RecruiterCompanyBindingTests(FoundationFixture fixture):IClassFixture<FoundationFixture>
{
    [Fact] public async Task ConcurrentCreationBindsOnlyOneCompanyAndLegacyCompaniesArePreserved()
    {
        var (recruiter,uid)=await fixture.User("Recruiter");
        var request=new { name="Single enterprise fixture",location="Hà Nội" };
        var responses=await Task.WhenAll(recruiter.PostAsJsonAsync("/api/companies",request),recruiter.PostAsJsonAsync("/api/companies",request));
        Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Created);Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        Assert.Equal(1,await db.Companies.CountAsync(x=>x.OwnerUserId==uid));
        // Pre-existing multi-company data is never silently removed by the new creation gate.
        db.Companies.Add(new Company {OwnerUserId=uid,Name="Legacy preserved",Slug="legacy-"+Guid.NewGuid().ToString("N")});await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await recruiter.PostAsJsonAsync("/api/companies",request)).StatusCode);
        Assert.Equal(2,await db.Companies.CountAsync(x=>x.OwnerUserId==uid));
    }
}
