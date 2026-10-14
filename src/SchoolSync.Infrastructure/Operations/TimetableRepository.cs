using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Operations;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Operations;

// Tenant-filtered by the CT-001 convention; no IgnoreQueryFilters().
public class TimetableRepository(SchoolSyncDbContext db) : ITimetableRepository
{
    public Task<bool> ClassExistsAsync(Guid id, CancellationToken ct) => db.Classes.AnyAsync(c => c.Id == id, ct);
    public Task<bool> SubjectExistsAsync(Guid id, CancellationToken ct) => db.Subjects.AnyAsync(s => s.Id == id, ct);
    public Task<bool> ActiveTeacherExistsAsync(Guid id, CancellationToken ct) => db.Teachers.AnyAsync(t => t.Id == id && t.IsActive, ct);

    public async Task<IReadOnlyList<TimetablePeriod>> PeriodsInSlotAsync(DayOfWeekSchool day, string timeSlot, Guid? exceptPeriodId, CancellationToken ct) =>
        await db.TimetablePeriods.Where(p => p.DayOfWeek == day && p.TimeSlot == timeSlot && (exceptPeriodId == null || p.Id != exceptPeriodId)).ToListAsync(ct);

    public Task<TimetablePeriod?> FindAsync(Guid id, CancellationToken ct) => db.TimetablePeriods.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(TimetablePeriod period, CancellationToken ct) => await db.TimetablePeriods.AddAsync(period, ct);

    public async Task<IReadOnlyList<TimetablePeriod>> ListAsync(Guid? classId, Guid? teacherId, bool publishedOnly, CancellationToken ct) =>
        await db.TimetablePeriods
            .Where(p => (classId == null || p.ClassId == classId) && (teacherId == null || p.TeacherId == teacherId) && (!publishedOnly || p.IsPublished))
            .OrderBy(p => p.DayOfWeek).ThenBy(p => p.TimeSlot).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
