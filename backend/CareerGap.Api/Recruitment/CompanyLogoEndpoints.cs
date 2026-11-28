using System.Security.Claims;
using CareerGap.Api.Data;
using CareerGap.Api.Files;
using Microsoft.EntityFrameworkCore;
namespace CareerGap.Api.Recruitment;
public sealed record CompanyLogoRequest(Guid? ImageId,long RowVersion);
public static class CompanyLogoEndpoints
{
    public static void MapCompanyLogos(this WebApplication app)
    {
        app.MapPut("/api/companies/{id:guid}/logo",async(Guid id,CompanyLogoRequest input,ClaimsPrincipal user,AppDb db,CancellationToken ct)=>{
            var uid=Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Id\"={uid} FOR UPDATE",ct);
            var company=await db.Companies.SingleOrDefaultAsync(x=>x.Id==id&&x.OwnerUserId==uid&&x.ArchivedAt==null,ct);if(company is null)return Results.NotFound();
            if(!await BusinessIdentityEndpoints.Primary(db,uid,id,ct))return Results.Problem(statusCode:409,title:"business_primary_required");
            if(company.RowVersion!=input.RowVersion)return Results.Problem(statusCode:409,title:"company_conflict");
            if(input.ImageId is {} imageId&&!await db.CommunityImages.AnyAsync(x=>x.Id==imageId&&x.OwnerId==uid,ct))return Results.NotFound();
            company.LogoImageId=input.ImageId;company.RowVersion++;company.UpdatedAt=DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.NoContent();
        }).RequireAuthorization(p=>p.RequireRole("Recruiter"));
        app.MapGet("/api/companies/{id:guid}/logo",async(Guid id,HttpContext http,AppDb db,IFileStorage storage,CancellationToken ct)=>{
            var uid=Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier),out var u)?u:(Guid?)null;
            var eligible=db.Companies.Where(c=>c.Id==id&&c.ArchivedAt==null&&c.LogoImageId!=null&&db.Users.Any(a=>a.Id==c.OwnerUserId&&a.AccountStatus=="active")&&
                (c.OwnerUserId==uid||db.BusinessIdentities.Any(b=>b.CompanyId==c.Id&&b.Status=="verified")&&db.Jobs.Any(j=>j.CompanyId==c.Id&&(j.Status=="published"||j.Status=="closed"))));
            var image=await (from c in eligible join i in db.CommunityImages on c.LogoImageId equals i.Id where i.OwnerId==c.OwnerUserId select i).AsNoTracking().SingleOrDefaultAsync(ct);
            if(image is null)return Results.NotFound();
            byte[] bytes;
            try { bytes = await storage.ReadAsync(image.ObjectKey, ct); }
            catch (StorageConfigurationException ex) { return Results.Problem(statusCode: 503, title: ex.Code); }
            catch (Exception ex) when (ex is IOException or Amazon.Runtime.AmazonServiceException or Amazon.Runtime.AmazonClientException or HttpRequestException or InvalidDataException) { return Results.Problem(statusCode: 503, title: "file_unavailable"); }
            if(!await eligible.AnyAsync(c=>c.LogoImageId==image.Id,ct))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";http.Response.Headers.XContentTypeOptions="nosniff";
            return Results.File(bytes,image.ContentType);
        });
    }
}
