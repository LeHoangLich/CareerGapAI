using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace CareerGap.Api.Community;
public sealed partial class CommunityService
{
    private async Task<SocialCareer> Career(Guid uid, bool visible, CancellationToken ct)
    {
        var roles=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId==uid select r.Name).ToArrayAsync(ct);
        var role=roles.Contains("Admin")?"Admin":roles.Contains("Recruiter")?"Recruiter":roles.Contains("Candidate")?"Candidate":"Member";
        var empty=new SocialCareer(role,null,null,null,null,[],null);
        if(!visible)return empty;
        if(role=="Candidate"){
            var p=await db.CandidateProfiles.AsNoTracking().Where(x=>x.UserId==uid)
                .Select(x=>new{x.Headline,x.DesiredRole,x.Location,x.ExperienceYears,x.Details}).SingleOrDefaultAsync(ct);
            if(p is null)return empty;
            using var details=JsonDocument.Parse(p.Details);
            var skills=details.RootElement.ValueKind==JsonValueKind.Object && details.RootElement.TryGetProperty("skills",out var list)&&list.ValueKind==JsonValueKind.Array
                ?list.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()!.Trim()).Where(x=>x.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray():[];
            return new(role,p.Headline,p.DesiredRole,p.Location,p.ExperienceYears,skills,null);
        }
        if(role=="Recruiter"){
            var company=await (from b in db.BusinessIdentities.AsNoTracking() join c in db.Companies on b.CompanyId equals c.Id
                where b.UserId==uid && b.Status=="verified" && c.OwnerUserId==uid && c.ArchivedAt==null
                select new SocialCompany(c.Id,c.Name,c.Description,c.Industry,c.SizeLabel,c.Location,c.WebsiteUrl,
                    c.LogoImageId!=null && db.Jobs.Any(j=>j.CompanyId==c.Id && (j.Status=="published"||j.Status=="closed"))?"/api/companies/"+c.Id+"/logo":null)).SingleOrDefaultAsync(ct);
            return empty with {Company=company};
        }
        return empty;
    }
}
