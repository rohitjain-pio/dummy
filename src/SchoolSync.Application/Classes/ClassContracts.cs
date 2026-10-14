using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Classes;

public record ClassResult(Guid Id, Guid InstitutionId, string Name, string Section, Guid? ClassTeacherId);

public record SubjectResult(Guid Id, Guid InstitutionId, string Name);

public enum ClassFailureReason
{
    None,
    ValidationFailed, // ERR-901
    NotFound, // ERR-902
    DuplicateNameSection, // ERR-305 (BRL-026)
    StudentsStillPlaced // ERR-306 (BRL-028)
}

public record ClassOutcome<T>(bool Succeeded, T? Value, ClassFailureReason Failure = ClassFailureReason.None)
{
    public static ClassOutcome<T> Ok(T value) => new(true, value);
    public static ClassOutcome<T> Fail(ClassFailureReason reason) => new(false, default, reason);
}

public interface IClassService
{
    Task<ClassOutcome<ClassResult>> CreateClassAsync(string name, string section, Guid institutionId, CancellationToken ct); // API-026
    Task<ClassOutcome<ClassResult>> UpdateClassAsync(Guid id, string name, string section, CancellationToken ct); // API-027
    Task<ClassOutcome<bool>> DeleteClassAsync(Guid id, CancellationToken ct); // API-027
    Task<ClassResult?> FindClassAsync(Guid id, CancellationToken ct); // API-028
    Task<IReadOnlyList<ClassResult>> ListClassesAsync(CancellationToken ct); // API-028

    Task<ClassOutcome<SubjectResult>> CreateSubjectAsync(string name, Guid institutionId, CancellationToken ct); // API-029
    Task<ClassOutcome<SubjectResult>> UpdateSubjectAsync(Guid id, string name, CancellationToken ct); // API-029
    Task<ClassOutcome<bool>> DeleteSubjectAsync(Guid id, CancellationToken ct); // API-029
    Task<IReadOnlyList<SubjectResult>> ListSubjectsAsync(CancellationToken ct);
}

public interface IClassRepository
{
    // BRL-026: (name, section) unique within institution. Tenant-filtered, so "within institution"
    // needs no clause of its own.
    Task<bool> NameSectionExistsAsync(string name, string section, Guid? excludingClassId, CancellationToken ct);

    // BRL-028: zero students currently placed. Counts withdrawn students too — a withdrawn
    // student still carries class_id, and SPEC-FR-014 requires their history stay attributable,
    // which deleting the class out from under them would break.
    Task<bool> HasStudentsPlacedAsync(Guid classId, CancellationToken ct);

    Task AddClassAsync(Class @class, CancellationToken ct);
    Task<Class?> FindClassAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Class>> ListClassesAsync(CancellationToken ct);
    Task RemoveClassAsync(Class @class, CancellationToken ct);

    Task AddSubjectAsync(Subject subject, CancellationToken ct);
    Task<Subject?> FindSubjectAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Subject>> ListSubjectsAsync(CancellationToken ct);
    Task RemoveSubjectAsync(Subject subject, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
