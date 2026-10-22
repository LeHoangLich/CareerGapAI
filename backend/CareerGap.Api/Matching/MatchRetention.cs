using CareerGap.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CareerGap.Api.Matching;

public static class MatchRetention
{
    // Caller owns the short purge transaction. Usage remains for reconciliation; document payloads do not.
    public static async Task Erase(AppDb db, IQueryable<MatchInput> inputs, CancellationToken ct)
    {
        var ids = await inputs.Select(x => x.AiJobId).ToArrayAsync(ct);
        if (ids.Length == 0) return;
        await db.Database.ExecuteSqlRawAsync("SET LOCAL careergap.account_purge = 'on'", ct);
        await db.MatchResults.Where(x => ids.Contains(x.AiJobId)).ExecuteDeleteAsync(ct);
        await db.MatchInputs.Where(x => ids.Contains(x.AiJobId)).ExecuteDeleteAsync(ct);
        await db.AiJobs.Where(x => ids.Contains(x.Id)).ExecuteUpdateAsync(s => s.SetProperty(x => x.OutputJson, (string?)null)
            .SetProperty(x => x.InputHash, ""), ct);
    }
}
