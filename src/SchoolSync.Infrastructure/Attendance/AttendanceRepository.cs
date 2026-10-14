using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Attendance;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Attendance;

// Every query here is tenant-filtered by the CT-001 convention, except the one method named
// ...AcrossInstitutions, which is ADR-0005's bounded exception.
public class AttendanceRepository(SchoolSyncDbContext db) : IAttendanceRepository
{
    public Task<string?> FindInstitutionTimeZoneAsync(Guid institutionId, CancellationToken ct) =>
        db.InstitutionConfigurations
            .Where(c => c.InstitutionId == institutionId)
            .Select(c => (string?)c.TimeZone)
            .FirstOrDefaultAsync(ct);

    public Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct) =>
        db.Classes.AnyAsync(c => c.Id == classId, ct);

    public Task<Guid?> FindTeacherIdForUserAsync(Guid userId, CancellationToken ct) =>
        db.UserAccounts.Where(u => u.Id == userId && u.IsActive).Select(u => u.TeacherId).FirstOrDefaultAsync(ct);

    public async Task<bool> TeacherTeachesClassAsync(Guid teacherId, Guid classId, CancellationToken ct) =>
        await db.Classes.AnyAsync(c => c.Id == classId && c.ClassTeacherId == teacherId, ct)
        || await db.SubjectTeacherClassMappings.AnyAsync(m => m.ClassId == classId && m.TeacherId == teacherId, ct);

    public async Task<IReadOnlyList<Student>> ListActiveStudentsInClassAsync(Guid classId, CancellationToken ct) =>
        await db.Students.Where(s => s.ClassId == classId && s.IsActive).OrderBy(s => s.RollNumber).ToListAsync(ct);

    public async Task<IReadOnlySet<Guid>> FindActiveTeacherIdsAsync(IReadOnlyCollection<Guid> teacherIds, CancellationToken ct) =>
        (await db.Teachers.Where(t => teacherIds.Contains(t.Id) && t.IsActive).Select(t => t.Id).ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyList<AttendanceRecord>> FindRecordsAsync(
        IReadOnlyCollection<Guid> personIds, DateOnly date, CancellationToken ct) =>
        await db.AttendanceRecords.Where(r => personIds.Contains(r.PersonId) && r.AttendanceDate == date).ToListAsync(ct);

    public async Task AddRecordAsync(AttendanceRecord record, CancellationToken ct) =>
        await db.AttendanceRecords.AddAsync(record, ct);

    public Task<AttendanceRecord?> FindRecordAsync(Guid recordId, CancellationToken ct) =>
        db.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == recordId, ct);

    public Task<Guid?> FindStudentClassIdAsync(Guid studentId, CancellationToken ct) =>
        db.Students.Where(s => s.Id == studentId).Select(s => s.ClassId).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<CorrectionRequest>> FindPendingRequestsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct) =>
        await db.CorrectionRequests
            .Where(r => recordIds.Contains(r.AttendanceRecordId) && r.Status == CorrectionRequestStatus.Pending)
            .ToListAsync(ct);

    public Task<CorrectionRequest?> FindRequestWithRecordAsync(Guid requestId, CancellationToken ct) =>
        db.CorrectionRequests.Include(r => r.AttendanceRecord).FirstOrDefaultAsync(r => r.Id == requestId, ct);

    public async Task AddRequestAsync(CorrectionRequest request, CancellationToken ct) =>
        await db.CorrectionRequests.AddAsync(request, ct);

    public async Task RemoveRecordAsync(AttendanceRecord record, CancellationToken ct)
    {
        // The requests are loaded and removed EXPLICITLY, not left to the FK cascade: a database-side
        // cascade deletes them inside SQL Server where the CT-003 interceptor never sees them, so
        // the request history would vanish with no audit row (BRL-035) — the same bypass class as
        // ExecuteDelete. The cascade stays in the schema only as a backstop.
        var requests = await db.CorrectionRequests.Where(r => r.AttendanceRecordId == record.Id).ToListAsync(ct);
        db.CorrectionRequests.RemoveRange(requests);
        db.AttendanceRecords.Remove(record); // not one of D8's five soft-deletable entities
    }

    public async Task<IReadOnlyList<AttendanceRecord>> SearchRecordsAsync(AttendanceQuery query, CancellationToken ct)
    {
        var records = db.AttendanceRecords.AsQueryable();
        if (query.PersonId is { } personId) records = records.Where(r => r.PersonId == personId);
        if (query.Date is { } date) records = records.Where(r => r.AttendanceDate == date);
        if (query.ClassId is { } classId)
        {
            var classStudentIds = db.Students.Where(s => s.ClassId == classId).Select(s => s.Id);
            records = records.Where(r => r.PersonType == PersonType.Student && classStudentIds.Contains(r.PersonId));
        }
        return await records.OrderByDescending(r => r.AttendanceDate).ThenBy(r => r.PersonId).ToListAsync(ct);
    }

    public Task<Guid?> FindStudentIdForUserAsync(Guid userId, CancellationToken ct) =>
        db.UserAccounts.Where(u => u.Id == userId && u.IsActive).Select(u => u.StudentId).FirstOrDefaultAsync(ct);

    // ParentStudentLink is not tenant-owned; joining through Students keeps the tenant boundary.
    public async Task<IReadOnlyList<Guid>> FindLinkedStudentIdsAsync(Guid parentUserId, CancellationToken ct) =>
        await db.ParentStudentLinks.Where(l => l.ParentUserId == parentUserId)
            .Join(db.Students, l => l.StudentId, s => s.Id, (l, s) => s.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AttendanceRecord>> FindRecordsForPersonsAsync(
        IReadOnlyCollection<Guid> personIds, DateOnly? date, CancellationToken ct) =>
        await db.AttendanceRecords
            .Where(r => personIds.Contains(r.PersonId) && (date == null || r.AttendanceDate == date))
            .OrderByDescending(r => r.AttendanceDate)
            .ToListAsync(ct);

    // ADR-0005: a further, argued IgnoreQueryFilters() (ADR-0002's rule). Bounded to Pending requests, and the
    // only caller (AttendanceService.RunLockSweepAsync) can only transition them to AutoRejected.
    public async Task<IReadOnlyList<PendingCorrection>> ListPendingCorrectionsAcrossInstitutionsAsync(CancellationToken ct)
    {
        var rows = await db.CorrectionRequests.IgnoreQueryFilters()
            .Where(r => r.Status == CorrectionRequestStatus.Pending)
            .Include(r => r.AttendanceRecord)
            .ToListAsync(ct);

        var institutionIds = rows.Select(r => r.InstitutionId).Distinct().ToList();
        var zones = await db.InstitutionConfigurations.IgnoreQueryFilters()
            .Where(c => institutionIds.Contains(c.InstitutionId))
            .ToDictionaryAsync(c => c.InstitutionId, c => c.TimeZone, ct);

        return rows.Select(r => new PendingCorrection(r, r.AttendanceRecord, zones.GetValueOrDefault(r.InstitutionId))).ToList();
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
