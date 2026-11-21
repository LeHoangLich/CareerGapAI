using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace CareerGap.Api.Tests;
public sealed partial class PairMatchingTests
{
    [Fact] public async Task CompanyLogoUsesSafeImageStorageOwnerAccessAndPublicVisibility()
    {
        var s=await Prepare();var (other,_)=await fixture.User("Recruiter");var guest=fixture.App.CreateClient();
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();var job=await db.Jobs.AsNoTracking().SingleAsync(x=>x.Id==s.Job);var company=await db.Companies.AsNoTracking().SingleAsync(x=>x.Id==job.CompanyId);
        using var bmp=new SkiaSharp.SKBitmap(10,10);bmp.Erase(SkiaSharp.SKColors.RoyalBlue);using var image=SkiaSharp.SKImage.FromBitmap(bmp);using var png=image.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
        using var form=new MultipartFormDataContent();form.Add(new ByteArrayContent(png.ToArray()),"file","logo.png");var upload=await s.Recruiter.PostAsync("/api/community/images",form);Assert.Equal(HttpStatusCode.Created,upload.StatusCode);var id=(await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var url=$"/api/companies/{company.Id}/logo";
        Assert.Equal(HttpStatusCode.NotFound,(await other.PutAsJsonAsync(url,new CompanyLogoRequest(id,company.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await s.Recruiter.PutAsJsonAsync(url,new CompanyLogoRequest(id,company.RowVersion))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await s.Recruiter.PutAsJsonAsync(url,new CompanyLogoRequest(null,company.RowVersion))).StatusCode);
        Assert.Equal("image/png",(await guest.GetAsync(url)).Content.Headers.ContentType!.MediaType);
        Assert.Contains("logoUrl",await guest.GetStringAsync($"/api/jobs/{s.Job}"));
        Assert.Equal(HttpStatusCode.Conflict,(await s.Recruiter.DeleteAsync($"/api/community/images/{id}")).StatusCode);
        var current=await db.Jobs.SingleAsync(x=>x.Id==s.Job);current.Status="archived";current.ClosedAt=DateTimeOffset.UtcNow;current.RowVersion++;await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound,(await guest.GetAsync(url)).StatusCode);Assert.Equal(HttpStatusCode.OK,(await s.Recruiter.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await s.Recruiter.PutAsJsonAsync(url,new CompanyLogoRequest(null,company.RowVersion+1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await s.Recruiter.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await s.Recruiter.DeleteAsync($"/api/community/images/{id}")).StatusCode);
    }
}
