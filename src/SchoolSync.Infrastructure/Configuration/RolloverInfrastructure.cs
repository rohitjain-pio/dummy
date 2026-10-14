using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolSync.Application.Attendance;
using SchoolSync.Application.Configuration;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Configuration;

// Tenant-filtered by the CT-001 convention; no IgnoreQueryFilters() (the job enters its scope first).
public class RolloverRepository(SchoolSyncDbContext db) : IRolloverRepository
{
    public Task<AcademicYear?> FindYearAsync(Guid id, CancellationToken ct) => db.AcademicYears.FirstOrDefaultAsync(y => y.Id == id, ct);
    public Task<AcademicYear?> FindYearStartingOnAsync(DateOnly start, CancellationToken ct) => db.AcademicYears.FirstOrDefaultAsync(y => y.StartDate == start, ct);
    public async Task<IReadOnlyList<AcademicYear>> ListYearsAsync(CancellationToken ct) => await db.AcademicYears.OrderBy(y => y.StartDate).ToListAsync(ct);
    public async Task AddYearAsync(AcademicYear year, CancellationToken ct) => await db.AcademicYears.AddAsync(year, ct);

    public Task<Class?> FindClassAsync(Guid id, CancellationToken ct) => db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyDictionary<Guid, Guid?>> ClassSuccessorsAsync(CancellationToken ct) =>
        await db.Classes.ToDictionaryAsync(c => c.Id, c => c.NextClassId, ct);

    public async Task<IReadOnlyList<Student>> ActiveStudentsWithClassAsync(CancellationToken ct) =>
        await db.Students.Where(s => s.IsActive && s.ClassId != null).ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> ActiveStudentIdsAsync(IReadOnlyCollection<Guid> candidateIds, CancellationToken ct) =>
        (await db.Students.Where(s => s.IsActive && candidateIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyList<Student>> StudentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Students.Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    // Enrollment is not tenant-owned; reaching it through the tenant-filtered AcademicYear keeps it scoped.
    public async Task<IReadOnlyList<Enrollment>> EnrollmentsAsync(Guid yearId, CancellationToken ct) =>
        await db.AcademicYears.Where(y => y.Id == yearId).SelectMany(y => y.Enrollments).ToListAsync(ct);

    public async Task AddEnrollmentAsync(Enrollment enrollment, CancellationToken ct) => await db.Enrollments.AddAsync(enrollment, ct);

    public async Task<IReadOnlySet<Guid>> HoldBackStudentIdsAsync(Guid yearId, CancellationToken ct) =>
        (await db.RolloverHoldBacks.Where(h => h.AcademicYearId == yearId).Select(h => h.StudentId).ToListAsync(ct)).ToHashSet();

    public async Task AddHoldBackAsync(RolloverHoldBack holdBack, CancellationToken ct) => await db.RolloverHoldBacks.AddAsync(holdBack, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

// ADR-0007: load the work item by id (the one cross-tenant read), enter its institution, then run.
public class RolloverJobRunner(SchoolSyncDbContext db, IRolloverService rollover, ILogger<RolloverJobRunner> logger)
{
    public async Task RunAsync(Guid academicYearId, CancellationToken ct)
    {
        var institutionId = await db.AcademicYears.IgnoreQueryFilters()
            .Where(y => y.Id == academicYearId).Select(y => (Guid?)y.InstitutionId).FirstOrDefaultAsync(ct);
        if (institutionId is null) return;

        using var _ = BackgroundTenantScope.Enter(institutionId.Value);
        var summary = await rollover.RunAsync(academicYearId, ct);
        if (summary is not null)
            logger.LogInformation("Rollover of academic year {YearId} -> {NextYearId}: {Promoted} promoted, {Held} held back, {Final} final-year.",
                summary.FromYearId, summary.ToYearId, summary.Promoted, summary.HeldBack, summary.FinalYear);
    }
}

public class RolloverJob(RolloverJobRunner runner)
{
    [AutomaticRetry(Attempts = 3)]
    public Task RunAsync(Guid academicYearId, CancellationToken ct) => runner.RunAsync(academicYearId, ct);
}

public class HangfireRolloverQueue(IBackgroundJobClient jobs) : IRolloverQueue
{
    public Task EnqueueAsync(Guid academicYearId, CancellationToken ct)
    {
        jobs.Enqueue<RolloverJob>(j => j.RunAsync(academicYearId, CancellationToken.None));
        return Task.CompletedTask;
    }
}

public class DisabledRolloverQueue(ILogger<DisabledRolloverQueue> logger) : IRolloverQueue
{
    public Task EnqueueAsync(Guid academicYearId, CancellationToken ct)
    {
        logger.LogWarning("Background jobs are disabled; rollover of academic year {YearId} will not run.", academicYearId);
        return Task.CompletedTask;
    }
}

// PF-012 trigger "the institution's configured academic-year end" (daily). Enumerates ended,
// not-yet-rolled-over years across institutions by id (ADR-0007: ids + institution only), then runs
// each inside its own institution's scope via RolloverJobRunner.
public class YearEndRolloverJob(SchoolSyncDbContext db, IServiceScopeFactory scopes, TimeProvider clock)
{
    public const string Id = "academic-year-rollover";

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var candidates = await db.AcademicYears.IgnoreQueryFilters()
            .Where(y => y.RolledOverAtUtc == null)
            .Select(y => new { y.Id, y.InstitutionId, y.EndDate })
            .ToListAsync(ct);
        var zones = await db.InstitutionConfigurations.IgnoreQueryFilters()
            .ToDictionaryAsync(c => c.InstitutionId, c => c.TimeZone, ct);

        var ran = 0;
        foreach (var year in candidates)
        {
            var today = AttendanceCalendar.LocalToday(clock.GetUtcNow(), AttendanceCalendar.ResolveTimeZone(zones.GetValueOrDefault(year.InstitutionId), out _));
            if (year.EndDate >= today) continue; // the year has not ended in that institution's calendar

            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<RolloverJobRunner>().RunAsync(year.Id, ct);
            ran++;
        }
        return ran;
    }
}
