using System.Net.Mail;
using System.Security.Claims;
using CareerGap.Api.Data;
using CareerGap.Api.Integrations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace CareerGap.Api.Recruitment;
public sealed class BusinessIdentity
{
    public Guid UserId {get;set;}
    public Guid CompanyId {get;set;}
    public string LegalName {get;set;}="";
    public string? TaxCode {get;set;}
    public string? BoundTaxCode {get;set;}
    public string Domain {get;set;}="";
    public string Status {get;set;}="draft";
    public string ReviewReason {get;set;}="";
    public long RowVersion {get;set;}
    public DateTimeOffset? SubmittedAt {get;set;}
    public Guid? ReviewedBy {get;set;}
    public DateTimeOffset? ReviewedAt {get;set;}
}
public sealed record BusinessSubmission(string LegalName,string TaxCode,long RowVersion);
public sealed record BusinessReview(string Action,string Reason,long RowVersion);
public static class BusinessIdentityEndpoints
{
    public static bool CorporateEmail(string? email)=>MailAddress.TryCreate(email,out var parsed)&&!new[]{"gmail.com","googlemail.com","yahoo.com","yahoo.com.vn","outlook.com","hotmail.com","live.com","icloud.com","aol.com","proton.me","protonmail.com","mail.com"}.Contains(parsed.Host.ToLowerInvariant());
    public static string? WebsiteDomain(string? url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.HostNameType==UriHostNameType.Dns?(uri.IdnHost.StartsWith("www.",StringComparison.OrdinalIgnoreCase)?uri.IdnHost[4..]:uri.IdnHost).ToLowerInvariant():null;
    public static bool DomainMatches(string? email,string domain)=>CorporateEmail(email)&&MailAddress.TryCreate(email,out var parsed)&&(parsed.Host.Equals(domain,StringComparison.OrdinalIgnoreCase)||parsed.Host.EndsWith("."+domain,StringComparison.OrdinalIgnoreCase));
    public static async Task<bool> Primary(AppDb db,Guid uid,Guid company,CancellationToken ct)=>await db.BusinessIdentities.AnyAsync(x=>x.UserId==uid&&x.CompanyId==company,ct);
    public static async Task<bool> Verified(AppDb db,Guid uid,Guid company,CancellationToken ct)=>await db.BusinessIdentities.AnyAsync(x=>x.UserId==uid&&x.CompanyId==company&&x.Status=="verified",ct);
    private static Guid Uid(ClaimsPrincipal user)=>Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static void MapBusinessIdentity(this WebApplication app)
    {
        var g=app.MapGroup("/api/recruiter/business").RequireAuthorization(p=>p.RequireRole("Recruiter"));
        g.MapGet("",async(ClaimsPrincipal user,AppDb db,CancellationToken ct)=>{
            var uid=Uid(user);var account=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==uid,ct);
            return Results.Ok(new{identity=await db.BusinessIdentities.AsNoTracking().SingleOrDefaultAsync(x=>x.UserId==uid,ct),email=account.Email,emailConfirmed=account.EmailConfirmed,corporateEmail=CorporateEmail(account.Email)});
        });
        g.MapPost("/primary/{companyId:guid}",async(Guid companyId,ClaimsPrincipal user,AppDb db,CancellationToken ct)=>{
            var uid=Uid(user);await using var tx=await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Id\"={uid} FOR UPDATE",ct);
            var old=await db.BusinessIdentities.SingleOrDefaultAsync(x=>x.UserId==uid,ct);
            if(old is not null)return old.CompanyId==companyId?Results.NoContent():Results.Problem(statusCode:409,title:"business_already_bound");
            if(!await db.Companies.AnyAsync(x=>x.Id==companyId&&x.OwnerUserId==uid&&x.ArchivedAt==null,ct))return Results.NotFound();
            db.BusinessIdentities.Add(new(){UserId=uid,CompanyId=companyId});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.NoContent();
        });
        g.MapPost("/email/request",async(ClaimsPrincipal user,UserManager<AppUser> users,IEmailSender sender,IConfiguration config,CancellationToken ct)=>{
            var account=await users.FindByIdAsync(Uid(user).ToString());if(account is null||!CorporateEmail(account.Email))return Results.Problem(statusCode:400,title:"business_email_required");
            if(account.EmailConfirmed)return Results.NoContent();
            var token=await users.GenerateEmailConfirmationTokenAsync(account);
            var origin=config["App:PublicOrigin"]??"http://127.0.0.1:5207";
            var link=new Uri(origin.TrimEnd('/')+"/?businessEmailToken="+Uri.EscapeDataString(token)+"#recruiter-company");
            try{await sender.SendBusinessVerificationAsync(account.Email!,link,ct);}catch(IntegrationError e){return Results.Problem(statusCode:503,title:e.Code);}
            return Results.NoContent();
        }).RequireRateLimiting("recovery");
        g.MapPost("/email/confirm",async(EmailConfirmation input,ClaimsPrincipal user,UserManager<AppUser> users)=>{
            var account=await users.FindByIdAsync(Uid(user).ToString());if(account is null||account.EmailConfirmed||string.IsNullOrWhiteSpace(input.Token))return Results.BadRequest();
            var result=await users.ConfirmEmailAsync(account,input.Token);return result.Succeeded?Results.NoContent():Results.BadRequest();
        });
        g.MapPut("",async(BusinessSubmission input,ClaimsPrincipal user,AppDb db,CancellationToken ct)=>{
            var uid=Uid(user);var tax=input.TaxCode?.Replace("-","").Trim();
            if(input.LegalName?.Trim().Length is not (>=3 and <=200)||tax is null||tax.Length is not (10 or 13)||!tax.All(char.IsAsciiDigit))return Results.Problem(statusCode:400,title:"business_input_invalid");
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            var row=await db.BusinessIdentities.FromSqlInterpolated($"SELECT * FROM business_identities WHERE user_id={uid} FOR UPDATE").SingleOrDefaultAsync(ct);if(row is null)return Results.Problem(statusCode:409,title:"business_primary_required");
            if(row.RowVersion!=input.RowVersion||row.Status is "pending" or "verified")return Results.Problem(statusCode:409,title:"business_conflict");
            if(row.BoundTaxCode is not null && row.BoundTaxCode!=tax)return Results.Problem(statusCode:409,title:"business_legal_entity_locked");
            var account=await db.Users.SingleAsync(x=>x.Id==uid,ct);var company=await db.Companies.SingleAsync(x=>x.Id==row.CompanyId&&x.OwnerUserId==uid&&x.ArchivedAt==null,ct);var domain=WebsiteDomain(company.WebsiteUrl);
            if(!account.EmailConfirmed||domain is null||!DomainMatches(account.Email,domain))return Results.Problem(statusCode:409,title:"business_domain_unverified");
            row.LegalName=input.LegalName!.Trim();row.TaxCode=tax;row.Domain=domain;row.Status="pending";row.ReviewReason="";row.RowVersion++;row.SubmittedAt=DateTimeOffset.UtcNow;
            try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateException e)when(e.InnerException is PostgresException{SqlState:"23505"}){return Results.Problem(statusCode:409,title:"business_tax_exists");}return Results.NoContent();
        });
        var admin=app.MapGroup("/api/admin/businesses").RequireAuthorization(p=>p.RequireRole("Admin"));
        admin.MapGet("",async(int? page,AppDb db,CancellationToken ct)=>{
            if(page is <1 or >10000)return Results.BadRequest();
            return Results.Ok(new{total=await db.BusinessIdentities.CountAsync(ct),items=await db.BusinessIdentities.AsNoTracking().OrderByDescending(x=>x.Status=="pending").ThenByDescending(x=>x.SubmittedAt).ThenBy(x=>x.UserId).Skip(((page??1)-1)*10).Take(10).Select(x=>new{x.UserId,x.CompanyId,x.LegalName,x.TaxCode,x.Domain,x.Status,x.RowVersion,x.ReviewReason,email=db.Users.Where(u=>u.Id==x.UserId).Select(u=>u.Email).FirstOrDefault(),website=db.Companies.Where(c=>c.Id==x.CompanyId).Select(c=>c.WebsiteUrl).FirstOrDefault()}).ToArrayAsync(ct)});
        });
        admin.MapPost("/{uid:guid}",async(Guid uid,BusinessReview input,ClaimsPrincipal user,AppDb db,CancellationToken ct)=>{
            if(input.Action is not("verify" or "reject")||input.Reason?.Trim().Length is not(>=3 and <=500))return Results.BadRequest();
            await using var tx=await db.Database.BeginTransactionAsync(ct);var row=await db.BusinessIdentities.FromSqlInterpolated($"SELECT * FROM business_identities WHERE user_id={uid} FOR UPDATE").SingleOrDefaultAsync(ct);if(row is null)return Results.NotFound();
            if(row.RowVersion!=input.RowVersion||row.Status!="pending")return Results.Problem(statusCode:409,title:"business_conflict");
            var company=await db.Companies.SingleAsync(x=>x.Id==row.CompanyId,ct);var account=await db.Users.SingleAsync(x=>x.Id==uid,ct);
            if(input.Action=="verify"&&(account.AccountStatus!="active"||!account.EmailConfirmed||company.ArchivedAt!=null||WebsiteDomain(company.WebsiteUrl)!=row.Domain||!DomainMatches(account.Email,row.Domain)))return Results.Problem(statusCode:409,title:"business_domain_unverified");
            if(input.Action=="verify")row.BoundTaxCode=row.TaxCode;
            row.Status=input.Action=="verify"?"verified":"rejected";row.ReviewReason=input.Reason!.Trim();row.RowVersion++;row.ReviewedAt=DateTimeOffset.UtcNow;row.ReviewedBy=Uid(user);
            db.AdminAudits.Add(new(){ActorId=Uid(user),TargetId=row.CompanyId,Action="business_"+input.Action,Reason=row.ReviewReason});await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.NoContent();
        });
    }
    public sealed record EmailConfirmation(string Token);
}
