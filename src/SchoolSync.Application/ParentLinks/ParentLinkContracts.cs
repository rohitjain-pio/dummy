using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.ParentLinks;

// API-016's response. The raw code is returned exactly once, here, at issue time — it is
// communicated out-of-band by staff (PF-005 step 2) and there is no endpoint that reads it back.
public record ParentLinkCodeIssued(Guid CodeId, string Code, DateTime ExpiresAtUtc);

// API-018's response.
public record ParentLinkCreated(Guid StudentId, DateTime LinkedAtUtc);

// API-019's response — the scoped student view a Student/Parent is allowed to see. Deliberately
// NOT StudentResult: BR-066 scopes a Parent to "linked children only", and re-using the full
// staff-facing shape here would hand a Parent the same projection leadership gets. Guardian
// contact fields in particular are staff data (BRL-021 ties them to the student record's own
// access rule, which a Parent does not hold).
public record LinkedStudent(Guid Id, string Name, string RollNumber, Guid? ClassId, bool IsActive);

public enum ParentLinkFailureReason
{
    None,
    NotFound, // ERR-902 — student or code does not exist within the caller's scope
    AlreadyConsumed, // ERR-307 — revoke/regenerate against a consumed code (terminal)
    Expired, // ERR-308
    InstitutionMismatch, // ERR-309
    NotValid // ERR-310 — code unknown, revoked, or already consumed
}

public record ParentLinkOutcome<T>(bool Succeeded, T? Value, ParentLinkFailureReason Failure = ParentLinkFailureReason.None)
{
    public static ParentLinkOutcome<T> Ok(T value) => new(true, value);
    public static ParentLinkOutcome<T> Fail(ParentLinkFailureReason reason) => new(false, default, reason);
}

public interface IParentLinkService
{
    Task<ParentLinkOutcome<ParentLinkCodeIssued>> IssueCodeAsync(Guid studentId, Guid issuedByUserId, CancellationToken ct); // API-016
    Task<ParentLinkOutcome<bool>> RevokeCodeAsync(Guid studentId, Guid codeId, CancellationToken ct); // API-017
    Task<ParentLinkOutcome<ParentLinkCreated>> ConsumeCodeAsync(string code, Guid parentUserId, Guid parentInstitutionId, CancellationToken ct); // API-018
    Task<LinkedStudent?> FindOwnStudentAsync(Guid callerUserId, CancellationToken ct); // API-019 (Student)
    Task<IReadOnlyList<LinkedStudent>> ListLinkedStudentsAsync(Guid parentUserId, CancellationToken ct); // API-019 (Parent)
}

public interface IParentLinkRepository
{
    // Tenant-filtered: staff may only issue a code against their own institution's student.
    Task<Student?> FindStudentAsync(Guid studentId, CancellationToken ct);

    // Codes currently in the Issued state for this student — used to invalidate the previous one
    // when staff regenerate (the state machine's "Issued --[regenerate]--> Issued, old code
    // invalidated" edge).
    Task<IReadOnlyList<ParentLinkCode>> FindIssuedCodesForStudentAsync(Guid studentId, DateTime nowUtc, CancellationToken ct);

    Task<ParentLinkCode?> FindCodeByIdAsync(Guid studentId, Guid codeId, CancellationToken ct);

    // CROSS-TENANT BY DESIGN — see ADR-0004. Resolves a submitted code and its student WITHOUT the
    // tenant filter, because the institution-mismatch answer (ERR-309) is only reachable if the
    // other institution's student can be seen at all. Returns the code and the student's owning
    // institution id, never the student's data.
    Task<(ParentLinkCode Code, Guid StudentInstitutionId)?> FindCodeForConsumptionAsync(string code, CancellationToken ct);

    Task<bool> LinkExistsAsync(Guid parentUserId, Guid studentId, CancellationToken ct);
    Task AddCodeAsync(ParentLinkCode code, CancellationToken ct);
    Task AddLinkAsync(ParentStudentLink link, CancellationToken ct);

    Task<Student?> FindStudentForUserAsync(Guid userAccountId, CancellationToken ct); // API-019 (Student)
    Task<IReadOnlyList<Student>> FindLinkedStudentsAsync(Guid parentUserId, CancellationToken ct); // API-019 (Parent)

    Task SaveChangesAsync(CancellationToken ct);
}
