using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolSync.Application.Commercial;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Commercial;

// PF-013 path 3b / FR-071 / BRL-071 (CT-504). Daily, on the same Hangfire recurring scheduler as
// the rollover (A4). For every trial past its expiry: the institution's plan becomes Entry and the
// trial is closed. That is the entire effect — this class has no dependency through which any
// billing, payment or charge could be made (TC-705 asserts it structurally).
// Institution is not tenant-owned, so no tenant scope is needed and none is bypassed.
public class TrialReversionJob(SchoolSyncDbContext db, TimeProvider clock, ILogger<TrialReversionJob> logger)
{
    public const string Id = "trial-reversion";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expired = await db.Institutions
            .Where(i => i.TrialPlanId != null && i.TrialExpiresAtUtc != null && i.TrialExpiresAtUtc <= now)
            .ToListAsync(ct);

        foreach (var institution in expired)
        {
            institution.SubscriptionPlanId = TrialService.EntryPlanId;
            institution.TrialPlanId = null;
            institution.TrialExpiresAtUtc = null; // TrialStartedAtUtc is kept: one trial per institution
        }

        if (expired.Count > 0) await db.SaveChangesAsync(ct); // each reversion audited (Institution is IAuditable)
        logger.LogInformation("Trial reversion: {Count} expired trial(s) reverted to the Entry plan.", expired.Count);
        return expired.Count;
    }
}
