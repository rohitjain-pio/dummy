using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Classes;

// API-026 – API-029. BRL-026 through BRL-029.
//
// Note on deletion: Class is NOT one of the five soft-deletable entities (07-business-logic/
// _shared.md §2 names Institution/Student/Teacher/Room/UserAccount, and Class carries no
// IsActive), so API-027's DELETE is a genuine row removal — which is exactly why BRL-028's
// zero-students guard is the thing standing between it and orphaned history.
public class ClassService(IClassRepository repository) : IClassService
{
    // API-026. BRL-027: belongs to exactly one institution, taken from the caller's claim.
    public async Task<ClassOutcome<ClassResult>> CreateClassAsync(string name, string section, Guid institutionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(section))
            return ClassOutcome<ClassResult>.Fail(ClassFailureReason.ValidationFailed);

        if (await repository.NameSectionExistsAsync(name, section, null, ct))
            return ClassOutcome<ClassResult>.Fail(ClassFailureReason.DuplicateNameSection);

        var @class = new Class { Id = Guid.NewGuid(), InstitutionId = institutionId, Name = name, Section = section };
        await repository.AddClassAsync(@class, ct);
        await repository.SaveChangesAsync(ct);
        return ClassOutcome<ClassResult>.Ok(ToResult(@class));
    }

    // API-027 (amend).
    public async Task<ClassOutcome<ClassResult>> UpdateClassAsync(Guid id, string name, string section, CancellationToken ct)
    {
        var @class = await repository.FindClassAsync(id, ct);
        if (@class is null) return ClassOutcome<ClassResult>.Fail(ClassFailureReason.NotFound);

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(section))
            return ClassOutcome<ClassResult>.Fail(ClassFailureReason.ValidationFailed);

        // Excluding itself, so renaming a class to what it already is isn't a duplicate.
        if (await repository.NameSectionExistsAsync(name, section, id, ct))
            return ClassOutcome<ClassResult>.Fail(ClassFailureReason.DuplicateNameSection);

        @class.Name = name;
        @class.Section = section;
        await repository.SaveChangesAsync(ct);
        return ClassOutcome<ClassResult>.Ok(ToResult(@class));
    }

    // API-027 (remove). BRL-028 — the guard runs before the removal, never after.
    public async Task<ClassOutcome<bool>> DeleteClassAsync(Guid id, CancellationToken ct)
    {
        var @class = await repository.FindClassAsync(id, ct);
        if (@class is null) return ClassOutcome<bool>.Fail(ClassFailureReason.NotFound);

        if (await repository.HasStudentsPlacedAsync(id, ct))
            return ClassOutcome<bool>.Fail(ClassFailureReason.StudentsStillPlaced);

        await repository.RemoveClassAsync(@class, ct);
        await repository.SaveChangesAsync(ct);
        return ClassOutcome<bool>.Ok(true);
    }

    public async Task<ClassResult?> FindClassAsync(Guid id, CancellationToken ct)
    {
        var @class = await repository.FindClassAsync(id, ct);
        return @class is null ? null : ToResult(@class);
    }

    // API-028. SPEC-FR-027 requires the class teacher be shown, which ClassResult carries.
    public async Task<IReadOnlyList<ClassResult>> ListClassesAsync(CancellationToken ct) =>
        (await repository.ListClassesAsync(ct)).Select(ToResult).ToList();

    // API-029. FR-028 — the subject catalogue. No uniqueness rule is declared for subject names
    // anywhere in the spec suite (BR-026 is about classes), so none is invented here.
    public async Task<ClassOutcome<SubjectResult>> CreateSubjectAsync(string name, Guid institutionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) return ClassOutcome<SubjectResult>.Fail(ClassFailureReason.ValidationFailed);

        var subject = new Subject { Id = Guid.NewGuid(), InstitutionId = institutionId, Name = name };
        await repository.AddSubjectAsync(subject, ct);
        await repository.SaveChangesAsync(ct);
        return ClassOutcome<SubjectResult>.Ok(new SubjectResult(subject.Id, subject.InstitutionId, subject.Name));
    }

    public async Task<ClassOutcome<SubjectResult>> UpdateSubjectAsync(Guid id, string name, CancellationToken ct)
    {
        var subject = await repository.FindSubjectAsync(id, ct);
        if (subject is null) return ClassOutcome<SubjectResult>.Fail(ClassFailureReason.NotFound);

        if (string.IsNullOrWhiteSpace(name)) return ClassOutcome<SubjectResult>.Fail(ClassFailureReason.ValidationFailed);

        subject.Name = name;
        await repository.SaveChangesAsync(ct);
        return ClassOutcome<SubjectResult>.Ok(new SubjectResult(subject.Id, subject.InstitutionId, subject.Name));
    }

    // RESOLVED SILENCE (recorded in the build log): no spec text and no ERR- covers deleting a
    // subject that still has teaching assignments, and the FK to SubjectTeacherClassMapping is
    // Restrict, so a naive delete would surface as a database exception rather than any catalogued
    // error. The mappings are pure join rows with no meaning independent of the subject, so
    // removing the subject removes them — see RemoveSubjectAsync. No new error code was invented
    // for a case 08-error-catalogue.md (a closed registry) does not name.
    public async Task<ClassOutcome<bool>> DeleteSubjectAsync(Guid id, CancellationToken ct)
    {
        var subject = await repository.FindSubjectAsync(id, ct);
        if (subject is null) return ClassOutcome<bool>.Fail(ClassFailureReason.NotFound);

        await repository.RemoveSubjectAsync(subject, ct);
        await repository.SaveChangesAsync(ct);
        return ClassOutcome<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<SubjectResult>> ListSubjectsAsync(CancellationToken ct) =>
        (await repository.ListSubjectsAsync(ct))
            .Select(s => new SubjectResult(s.Id, s.InstitutionId, s.Name)).ToList();

    private static ClassResult ToResult(Class c) => new(c.Id, c.InstitutionId, c.Name, c.Section, c.ClassTeacherId);
}
