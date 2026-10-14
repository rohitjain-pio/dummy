using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Operations;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Operations;

// Tenant-filtered by the CT-001 convention, except CountAcrossInstitutionsAsync (ADR-0006).
public class DashboardRepository(SchoolSyncDbContext db) : IDashboardRepository
{
    public Task<string?> FindInstitutionTimeZoneAsync(Guid institutionId, CancellationToken ct) =>
        db.InstitutionConfigurations.Where(c => c.InstitutionId == institutionId).Select(c => (string?)c.TimeZone).FirstOrDefaultAsync(ct);

    public Task<Guid?> FindTeacherIdForUserAsync(Guid userId, CancellationToken ct) =>
        db.UserAccounts.Where(u => u.Id == userId && u.IsActive).Select(u => u.TeacherId).FirstOrDefaultAsync(ct);

    public Task<Guid?> FindStudentIdForUserAsync(Guid userId, CancellationToken ct) =>
        db.UserAccounts.Where(u => u.Id == userId && u.IsActive).Select(u => u.StudentId).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindLinkedStudentIdsAsync(Guid parentUserId, CancellationToken ct) =>
        await db.ParentStudentLinks.Where(l => l.ParentUserId == parentUserId)
            .Join(db.Students, l => l.StudentId, s => s.Id, (l, s) => s.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Class>> ListClassesAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct) =>
        await db.Classes.Where(c => onlyClassIds == null || onlyClassIds.Contains(c.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListTaughtClassIdsAsync(Guid teacherId, CancellationToken ct) =>
        (await db.Classes.Where(c => c.ClassTeacherId == teacherId).Select(c => c.Id).ToListAsync(ct))
        .Union(await db.SubjectTeacherClassMappings.Where(m => m.TeacherId == teacherId).Select(m => m.ClassId).ToListAsync(ct))
        .ToList();

    public async Task<IReadOnlyList<Student>> ListActiveStudentsAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct) =>
        await db.Students.Where(s => s.IsActive && (onlyClassIds == null || (s.ClassId != null && onlyClassIds.Contains(s.ClassId.Value)))).ToListAsync(ct);

    public async Task<IReadOnlyList<Student>> FindStudentsAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken ct) =>
        await db.Students.Where(s => studentIds.Contains(s.Id)).ToListAsync(ct);

    public Task<int> CountActiveTeachersAsync(CancellationToken ct) => db.Teachers.CountAsync(t => t.IsActive, ct);

    public async Task<IReadOnlyList<AttendanceFact>> ListAttendanceAsync(
        IReadOnlyCollection<Guid>? onlyPersonIds, PersonType? personType, DateOnly? from, DateOnly to, CancellationToken ct) =>
        await db.AttendanceRecords
            .Where(r => (onlyPersonIds == null || onlyPersonIds.Contains(r.PersonId))
                        && (personType == null || r.PersonType == personType)
                        && (from == null || r.AttendanceDate >= from) && r.AttendanceDate <= to)
            .Select(r => new AttendanceFact(r.PersonId, r.PersonType, r.AttendanceDate, r.Outcome))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Announcement>> ListRecentAnnouncementsAsync(DateOnly today, int take, CancellationToken ct) =>
        await db.Announcements.Where(a => !a.IsWithdrawn && a.EffectiveDate <= today)
            .OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.CreatedAtUtc).Take(take).ToListAsync(ct);

    public async Task<IReadOnlyList<Announcement>> ListUpcomingAnnouncementsAsync(DateOnly today, int take, CancellationToken ct) =>
        await db.Announcements.Where(a => !a.IsWithdrawn && a.EffectiveDate > today)
            .OrderBy(a => a.EffectiveDate).Take(take).ToListAsync(ct);

    // ADR-0006: API-049 is anonymous and platform-wide by definition (BR-049), so it cannot be
    // tenant-scoped. Bounded to four COUNTs — no row, name or per-institution figure leaves here.
    public async Task<PlatformSummary> CountAcrossInstitutionsAsync(CancellationToken ct) => new(
        await db.Institutions.CountAsync(i => i.IsActive, ct),
        await db.Students.IgnoreQueryFilters().CountAsync(s => s.IsActive, ct),
        await db.Teachers.IgnoreQueryFilters().CountAsync(t => t.IsActive, ct),
        await db.Classes.IgnoreQueryFilters().CountAsync(ct));
}
