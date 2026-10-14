using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SchoolSync.Application.Attendance;
using SchoolSync.Application.Configuration;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Configuration;

// Tenant-filtered (InstitutionConfiguration is ITenantOwned).
public class InstitutionConfigRepository(SchoolSyncDbContext db) : IInstitutionConfigRepository
{
    public Task<InstitutionConfiguration?> FindAsync(Guid institutionId, CancellationToken ct) =>
        db.InstitutionConfigurations.FirstOrDefaultAsync(c => c.InstitutionId == institutionId, ct);

    public async Task AddAsync(InstitutionConfiguration configuration, CancellationToken ct) =>
        await db.InstitutionConfigurations.AddAsync(configuration, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

// CT-501: the daily recurring jobs (Tech Design §3.5), registered with Hangfire at startup.
public static class RecurringJobIds
{
    public const string AttendanceLockSweep = "attendance-lock-sweep"; // CT-202 / Q-206 / Q-216
    public const string RetentionSweep = "retention-sweep"; // _shared.md §4 (scaffold)
}

// Q-216 closed: CT-202's sweep logic, now on a daily schedule.
public class AttendanceLockSweepJob(IAttendanceService attendance, ILogger<AttendanceLockSweepJob> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var swept = await attendance.RunLockSweepAsync(ct);
        logger.LogInformation("Attendance lock sweep auto-rejected {Count} pending correction request(s).", swept);
    }
}

// _shared.md §4 / CMP-001: SCAFFOLD ONLY (the task row says "job scaffold"). It finds the
// Student/Teacher/UserAccount rows past the 7-year post-deactivation clock (CFG-007) and logs the
// counts. It deliberately anonymises and deletes NOTHING: which fields are anonymised versus deleted
// is not specified, and a guessed irreversible PII operation is exactly what must never ship
// silently. Recorded in the build log and _upstream-feedback.md.
public class RetentionSweepJob(SchoolSyncDbContext db, TimeProvider clock, ILogger<RetentionSweepJob> logger)
{
    public const int RetentionYears = 7; // CFG-007

    public async Task<(int Students, int Teachers, int Accounts)> RunAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddYears(-RetentionYears);
        // Platform-wide by nature (no tenant, no caller): COUNT-only reads — ADR-0008.
        var result = (
            await db.Students.IgnoreQueryFilters().CountAsync(s => !s.IsActive && s.DeactivatedAtUtc != null && s.DeactivatedAtUtc <= cutoff, ct),
            await db.Teachers.IgnoreQueryFilters().CountAsync(t => !t.IsActive && t.DeactivatedAtUtc != null && t.DeactivatedAtUtc <= cutoff, ct),
            await db.UserAccounts.IgnoreQueryFilters().CountAsync(u => !u.IsActive && u.DeactivatedAtUtc != null && u.DeactivatedAtUtc <= cutoff, ct));
        logger.LogWarning(
            "Retention sweep (scaffold, no action taken): {Students} student, {Teachers} teacher and {Accounts} account record(s) are past the {Years}-year retention period.",
            result.Item1, result.Item2, result.Item3, RetentionYears);
        return result;
    }
}
