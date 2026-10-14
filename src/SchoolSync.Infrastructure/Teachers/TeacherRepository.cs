using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Teachers;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Teachers;

// Every query is tenant-filtered by the CT-001 convention; none calls IgnoreQueryFilters().
public class TeacherRepository(SchoolSyncDbContext db) : ITeacherRepository
{
    public async Task AddAsync(Teacher teacher, CancellationToken ct) =>
        await db.Teachers.AddAsync(teacher, ct);

    public Task<Teacher?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Teachers.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<Teacher>> ListAsync(CancellationToken ct) =>
        await db.Teachers.OrderBy(t => t.Name).ToListAsync(ct);

    public Task<UserAccount?> FindUserAccountAsync(Guid userAccountId, CancellationToken ct) =>
        db.UserAccounts.FirstOrDefaultAsync(u => u.Id == userAccountId, ct);

    public Task<Class?> FindClassAsync(Guid classId, CancellationToken ct) =>
        db.Classes.FirstOrDefaultAsync(c => c.Id == classId, ct);

    // BRL-023, teacher side. `exceptClassId` keeps a re-assignment to the teacher's OWN class
    // from being reported as a conflict against itself.
    public Task<bool> IsClassTeacherElsewhereAsync(Guid teacherId, Guid exceptClassId, CancellationToken ct) =>
        db.Classes.AnyAsync(c => c.ClassTeacherId == teacherId && c.Id != exceptClassId, ct);

    // BRL-025. Both holdings count: a class-teacher assignment and any subject+class mapping.
    // TimetablePeriod also carries a TeacherId, but timetabling is P4 and BR-025 names only
    // "class-teacher/subject assignment" — when P4 lands, this method is the one place that has
    // to grow, which is why the check is here rather than inline in the service.
    public async Task<bool> HasActiveAssignmentsAsync(Guid teacherId, CancellationToken ct) =>
        await db.Classes.AnyAsync(c => c.ClassTeacherId == teacherId, ct)
        || await db.SubjectTeacherClassMappings.AnyAsync(m => m.TeacherId == teacherId, ct);

    public Task<bool> SubjectExistsAsync(Guid subjectId, CancellationToken ct) =>
        db.Subjects.AnyAsync(s => s.Id == subjectId, ct);

    public Task<bool> SubjectAssignmentExistsAsync(Guid subjectId, Guid teacherId, Guid classId, CancellationToken ct) =>
        db.SubjectTeacherClassMappings.AnyAsync(
            m => m.SubjectId == subjectId && m.TeacherId == teacherId && m.ClassId == classId, ct);

    public async Task AddSubjectAssignmentAsync(SubjectTeacherClassMapping mapping, CancellationToken ct) =>
        await db.SubjectTeacherClassMappings.AddAsync(mapping, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
