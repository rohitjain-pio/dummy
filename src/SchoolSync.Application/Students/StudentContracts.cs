using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Students;

// API-011's request payload, minus the transport concerns. ClassId is non-optional here because
// API-011 lists `classId` without the `?` that marks every genuinely optional field in the same
// contract line (guardianFatherName?, guardianMotherName?, guardianContactEmail?, guardianPhone?),
// and SPEC-FR-011 requires "class must exist in caller's institution". The Student.ClassId COLUMN
// stays nullable (CT-001, "Required once enrolled; BR-019 gates attendance on this, not enrolment
// itself") — the API requires a placement at enrolment, the schema tolerates its absence. Both
// statements are true and neither was weakened to fit the other.
public record StudentFields(
    string SrNumber,
    string RollNumber,
    string Name,
    DateOnly DateOfBirth,
    string Gender,
    string Email,
    Guid ClassId,
    string? GuardianFatherName,
    string? GuardianMotherName,
    string? GuardianContactEmail,
    string? GuardianPhone);

// BR-021/BRL-021: the guardian fields sit in this one result shape alongside the rest of the
// record — deliberately NOT split into a separate, separately-fetchable projection. "Same access
// rule as the student record itself (no separate, looser rule)" is enforced structurally: there
// is no path that returns guardian data which is not this type, returned from an [Authorize]d
// endpoint.
public record StudentResult(
    Guid Id,
    Guid InstitutionId,
    string SrNumber,
    string RollNumber,
    string Name,
    DateOnly DateOfBirth,
    string Gender,
    string Email,
    Guid? ClassId,
    string? GuardianFatherName,
    string? GuardianMotherName,
    string? GuardianContactEmail,
    string? GuardianPhone,
    bool IsActive,
    DateTime? DeactivatedAtUtc);

public enum StudentFailureReason
{
    None,
    ValidationFailed, // ERR-901
    DuplicateIdentifier, // ERR-301 (BRL-018) — SR number OR roll number already in use
    ClassNotFound, // ERR-901 — SPEC-FR-011, class must exist in caller's institution
    NotFound, // ERR-902
    CapacityExceeded // ERR-801 — BRL-073 tier learner-capacity gate (CT-503)
}

// Same shape as CT-101's InstitutionResult2<T>. Named Outcome rather than Result2 only because
// there is no name collision to work around here — Result2 was a workaround for InstitutionResult
// already taking the name, not a convention worth propagating.
public record StudentOutcome<T>(bool Succeeded, T? Value, StudentFailureReason Failure = StudentFailureReason.None)
{
    public static StudentOutcome<T> Ok(T value) => new(true, value);
    public static StudentOutcome<T> Fail(StudentFailureReason reason) => new(false, default, reason);
}

public interface IStudentService
{
    Task<StudentOutcome<StudentResult>> EnrolAsync(StudentFields fields, Guid institutionId, CancellationToken ct); // API-011
    Task<StudentOutcome<StudentResult>> AmendAsync(Guid id, StudentFields fields, CancellationToken ct); // API-012
    Task<StudentOutcome<StudentResult>> WithdrawAsync(Guid id, CancellationToken ct); // API-013
    Task<StudentOutcome<StudentResult>> ReactivateAsync(Guid id, CancellationToken ct); // API-013 mirror
    Task<StudentOutcome<StudentResult>> LinkAccountAsync(Guid id, Guid userAccountId, CancellationToken ct); // API-015
    Task<StudentResult?> FindAsync(Guid id, CancellationToken ct); // API-014
    Task<IReadOnlyList<StudentResult>> ListAsync(CancellationToken ct); // API-014
    Task<IReadOnlyList<StudentResult>> RosterAsync(Guid classId, CancellationToken ct); // API-014
}

// Every query below runs under the global tenant query filter (T-002/D4), so "within institution"
// — BR-018's uniqueness scope, BRL-020's access scope — is structural here, not a WHERE clause
// each method has to remember to add. None of these call IgnoreQueryFilters().
public interface IStudentRepository
{
    // BRL-018. Deliberately includes withdrawn (IsActive=false) students: the DB unique index is
    // (InstitutionId, SrNumber) with no filter, so an application check that skipped inactive rows
    // would accept a payload the database then rejects with a constraint violation.
    Task<bool> SrNumberExistsAsync(string srNumber, Guid? excludingStudentId, CancellationToken ct);
    Task<bool> RollNumberExistsAsync(string rollNumber, Guid? excludingStudentId, CancellationToken ct);

    Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct);
    Task AddAsync(Student student, CancellationToken ct);
    Task<Student?> FindByIdAsync(Guid id, CancellationToken ct);
    Task<UserAccount?> FindUserAccountAsync(Guid userAccountId, CancellationToken ct);
    Task<IReadOnlyList<Student>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<Student>> RosterAsync(Guid classId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
