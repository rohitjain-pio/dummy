using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Teachers;

// API-020's payload (FR-018's field list).
public record TeacherFields(
    string Name,
    string ContactEmail,
    string Telephone,
    string Gender,
    string Address,
    DateOnly JoiningDate);

public record TeacherResult(
    Guid Id,
    Guid InstitutionId,
    string Name,
    string ContactEmail,
    string Telephone,
    string Gender,
    string Address,
    DateOnly JoiningDate,
    bool IsActive,
    DateTime? DeactivatedAtUtc);

public record SubjectAssignment(Guid SubjectId, Guid TeacherId, Guid ClassId);

public enum TeacherFailureReason
{
    None,
    ValidationFailed, // ERR-901
    NotFound, // ERR-902
    AssignmentConflict, // ERR-303 (BRL-023)
    HasActiveAssignments // ERR-304 (BRL-025)
}

public record TeacherOutcome<T>(bool Succeeded, T? Value, TeacherFailureReason Failure = TeacherFailureReason.None)
{
    public static TeacherOutcome<T> Ok(T value) => new(true, value);
    public static TeacherOutcome<T> Fail(TeacherFailureReason reason) => new(false, default, reason);
}

public interface ITeacherService
{
    Task<TeacherOutcome<TeacherResult>> RegisterAsync(TeacherFields fields, Guid institutionId, CancellationToken ct); // API-020
    Task<TeacherOutcome<TeacherResult>> AmendAsync(Guid id, TeacherFields fields, CancellationToken ct); // API-021
    Task<TeacherOutcome<TeacherResult>> RemoveAsync(Guid id, CancellationToken ct); // API-022
    Task<TeacherOutcome<TeacherResult>> ReinstateAsync(Guid id, CancellationToken ct); // API-022 mirror
    Task<TeacherOutcome<TeacherResult>> LinkAccountAsync(Guid id, Guid userAccountId, CancellationToken ct); // SPEC-FR-023
    Task<TeacherResult?> FindAsync(Guid id, CancellationToken ct); // API-023
    Task<IReadOnlyList<TeacherResult>> ListAsync(CancellationToken ct); // API-023

    // API-024. A null teacherId RELEASES the class's current class teacher — see the note in
    // TeacherService; BRL-023's "until the first is released" requires a release path and this
    // endpoint is the only one the contract gives.
    Task<TeacherOutcome<bool>> SetClassTeacherAsync(Guid classId, Guid? teacherId, CancellationToken ct);

    Task<TeacherOutcome<SubjectAssignment>> AssignSubjectAsync(Guid subjectId, Guid teacherId, Guid classId, CancellationToken ct); // API-025
}

// All queries run under the tenant filter (Teacher, Class, Subject and
// SubjectTeacherClassMapping's parents are all ITenantOwned), so institution scoping is
// structural throughout — no method adds its own WHERE, and none calls IgnoreQueryFilters().
public interface ITeacherRepository
{
    Task AddAsync(Teacher teacher, CancellationToken ct);
    Task<Teacher?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Teacher>> ListAsync(CancellationToken ct);
    Task<UserAccount?> FindUserAccountAsync(Guid userAccountId, CancellationToken ct);

    Task<Class?> FindClassAsync(Guid classId, CancellationToken ct);

    // BRL-023, the teacher side: is this teacher already class teacher of some OTHER class?
    Task<bool> IsClassTeacherElsewhereAsync(Guid teacherId, Guid exceptClassId, CancellationToken ct);

    // BRL-025: any class-teacher holding, or any subject+class mapping.
    Task<bool> HasActiveAssignmentsAsync(Guid teacherId, CancellationToken ct);

    Task<bool> SubjectExistsAsync(Guid subjectId, CancellationToken ct);
    Task<bool> SubjectAssignmentExistsAsync(Guid subjectId, Guid teacherId, Guid classId, CancellationToken ct);
    Task AddSubjectAssignmentAsync(SubjectTeacherClassMapping mapping, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
