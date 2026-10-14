using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Classes;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Classes;

// Tenant-filtered throughout; no IgnoreQueryFilters() anywhere.
public class ClassRepository(SchoolSyncDbContext db) : IClassRepository
{
    // BRL-026. Mirrors the unique index (InstitutionId, Name, Section) exactly.
    public Task<bool> NameSectionExistsAsync(string name, string section, Guid? excludingClassId, CancellationToken ct)
    {
        var query = db.Classes.Where(c => c.Name == name && c.Section == section);
        if (excludingClassId is { } id) query = query.Where(c => c.Id != id);
        return query.AnyAsync(ct);
    }

    // BRL-028. Withdrawn students count: Student has no soft-delete query filter, so this sees
    // them, and that is deliberate — a withdrawn student still references the class and
    // SPEC-FR-014 requires their history remain attributable.
    // CT-502: the same reasoning extends to enrolment history — a class that appears in any
    // (append-only, BR-060) Enrollment row is still carrying attributable history, so it is refused
    // with ERR-306 rather than failing on the Enrollment foreign key.
    public async Task<bool> HasStudentsPlacedAsync(Guid classId, CancellationToken ct) =>
        await db.Students.AnyAsync(s => s.ClassId == classId, ct)
        || await db.Enrollments.AnyAsync(e => e.ClassId == classId, ct);

    public async Task AddClassAsync(Class @class, CancellationToken ct) =>
        await db.Classes.AddAsync(@class, ct);

    public Task<Class?> FindClassAsync(Guid id, CancellationToken ct) =>
        db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Class>> ListClassesAsync(CancellationToken ct) =>
        await db.Classes.OrderBy(c => c.Name).ThenBy(c => c.Section).ToListAsync(ct);

    // A real removal — Class is not soft-deletable (see ClassService's note). CT-004's
    // static-analysis guard test forbids .Remove()/.RemoveRange() on the FIVE soft-deletable
    // entities specifically; Class is not one of them, so this is the intended path for
    // API-027's DELETE and not a violation of D8.
    public async Task RemoveClassAsync(Class @class, CancellationToken ct)
    {
        // CT-502 (Q-229): a class that is another class's rollover successor stops being one —
        // that predecessor becomes "final year" rather than pointing at a class that no longer exists.
        foreach (var predecessor in await db.Classes.Where(c => c.NextClassId == @class.Id).ToListAsync(ct))
            predecessor.NextClassId = null;
        db.Classes.Remove(@class);
    }

    public async Task AddSubjectAsync(Subject subject, CancellationToken ct) =>
        await db.Subjects.AddAsync(subject, ct);

    public Task<Subject?> FindSubjectAsync(Guid id, CancellationToken ct) =>
        db.Subjects.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Subject>> ListSubjectsAsync(CancellationToken ct) =>
        await db.Subjects.OrderBy(s => s.Name).ToListAsync(ct);

    // The join rows go with the subject: the FK is Restrict, so leaving them would turn a subject
    // deletion into a database exception instead of a clean result. They carry no meaning without
    // the subject they map.
    public async Task RemoveSubjectAsync(Subject subject, CancellationToken ct)
    {
        var mappings = await db.SubjectTeacherClassMappings.Where(m => m.SubjectId == subject.Id).ToListAsync(ct);
        db.SubjectTeacherClassMappings.RemoveRange(mappings);
        db.Subjects.Remove(subject);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
