using System.Security.Claims;
using System.Text.Json;
using CareerGap.Api.Data;
using CareerGap.Api.Matching;
using Microsoft.EntityFrameworkCore;
namespace CareerGap.Api.Learning;

public sealed class SavedRoadmap
{
    public Guid PairId { get; set; }
    public string Plan { get; set; } = "{}";
    public string Completed { get; set; } = "[]";
    public long RowVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
public sealed record SaveRoadmapRequest(int Weeks, int HoursPerWeek, long RowVersion);
public sealed record RoadmapProgressRequest(int Week, string RequirementKey, bool Completed, long RowVersion);
public static class SavedRoadmapEndpoints
{
    private static object View(SavedRoadmap row) => new { plan = JsonSerializer.Deserialize<WeeklyPlan>(row.Plan), completed = JsonSerializer.Deserialize<string[]>(row.Completed), row.RowVersion, row.UpdatedAt };
    private static async Task<bool> Allowed(Guid uid, Guid id, PairMatchingService pairs, CancellationToken ct) => await pairs.Read(uid,id,ct) is { Context: "self", Status: "succeeded", Criteria: not null };
    public static void MapSavedRoadmap(this WebApplication app)
    {
        var group = app.MapGroup("/api/learning/saved-roadmaps").RequireAuthorization(p=>p.RequireRole("Candidate"));
        group.MapGet("/{id:guid}",async (Guid id,ClaimsPrincipal user,AppDb db,PairMatchingService pairs,CancellationToken ct)=>{
            var uid=Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if(!await Allowed(uid,id,pairs,ct))return Results.NotFound();
            var row=await db.SavedRoadmaps.AsNoTracking().SingleOrDefaultAsync(x=>x.PairId==id,ct);
            if(!await Allowed(uid,id,pairs,ct))return Results.NotFound();
            return Results.Ok(new { saved = row is null ? null : View(row) });
        });
        group.MapPut("/{id:guid}",async (Guid id,SaveRoadmapRequest input,ClaimsPrincipal user,AppDb db,PairMatchingService pairs,TimeProvider clock,CancellationToken ct)=>{
            if(input.Weeks is <2 or >12 || input.HoursPerWeek is <1 or >20 || input.RowVersion<0)return Results.BadRequest();
            var uid=Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if(!await Allowed(uid,id,pairs,ct))return Results.NotFound();
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"roadmap:"+id},0))",ct);
            var pair=await pairs.Read(uid,id,ct);
            if(pair is not {Context:"self",Status:"succeeded",Criteria:not null})return Results.NotFound();
            var row=await db.SavedRoadmaps.SingleOrDefaultAsync(x=>x.PairId==id,ct);
            if((row?.RowVersion??0)!=input.RowVersion)return Results.Problem(statusCode:409,title:"roadmap_conflict");
            var plan=WeeklyRoadmap.Build(id,pair.Criteria,input.Weeks,input.HoursPerWeek,pair.CareerContext);
            if(plan.Schedule.Count==0)return Results.Problem(statusCode:409,title:"roadmap_no_gaps");
            if(row is null){row=new(){PairId=id};db.SavedRoadmaps.Add(row);}
            var encoded=JsonSerializer.Serialize(plan);
            if(JsonSerializer.Deserialize<WeeklyPlan>(row.Plan) is not { } old || old.Weeks!=plan.Weeks || old.HoursPerWeek!=plan.HoursPerWeek || old.Method!=plan.Method || old.CareerContext!=plan.CareerContext){row.Plan=encoded;row.Completed="[]";row.RowVersion++;row.UpdatedAt=clock.GetUtcNow();}
            if(!await Allowed(uid,id,pairs,ct))return Results.NotFound();
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok(View(row));
        });
        group.MapPut("/{id:guid}/progress",async (Guid id,RoadmapProgressRequest input,ClaimsPrincipal user,AppDb db,PairMatchingService pairs,TimeProvider clock,CancellationToken ct)=>{
            var uid=Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if(!await Allowed(uid,id,pairs,ct))return Results.NotFound();
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"roadmap:"+id},0))",ct);
            var row=await db.SavedRoadmaps.SingleOrDefaultAsync(x=>x.PairId==id,ct);if(row is null)return Results.NotFound();
            if(row.RowVersion!=input.RowVersion)return Results.Problem(statusCode:409,title:"roadmap_conflict");
            var plan=JsonSerializer.Deserialize<WeeklyPlan>(row.Plan)!;
            if(!plan.Schedule.Any(w=>w.Week==input.Week&&w.Focus.Any(f=>f.RequirementKey==input.RequirementKey)))return Results.BadRequest();
            var completed=JsonSerializer.Deserialize<HashSet<string>>(row.Completed)!;var key=input.Week+":"+input.RequirementKey;
            var changed=input.Completed?completed.Add(key):completed.Remove(key);
            if(changed){row.Completed=JsonSerializer.Serialize(completed.OrderBy(x=>x,StringComparer.Ordinal));row.RowVersion++;row.UpdatedAt=clock.GetUtcNow();}
            if(!await Allowed(uid,id,pairs,ct))return Results.NotFound();
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return Results.Ok(View(row));
        });
    }
}
