using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Attendance;

// Who is calling. Role and institution come from the JWT; the Teacher row (if any) is resolved
// from UserAccount.TeacherId by the service, never trusted from the request.
public record AttendanceCaller(Guid UserId, UserRole Role, Guid InstitutionId)
{
    public bool IsLeadership => Role is UserRole.Administrator or UserRole.Principal or UserRole.SchoolInCharge;
}

public record RosterEntry(Guid StudentId, string Name, AttendanceOutcome? CurrentOutcome);

// API-032's entry. Outcome and person type arrive as strings and are parsed by the service, so an
// unknown value is VAL-013 -> ERR-901 rather than a framework model-binding 400 with no catalogue body.
public record MarkEntry(Guid PersonId, string? PersonType, string? Outcome);

public record MarkRequest(Guid? ClassId, DateOnly Date, IReadOnlyList<MarkEntry> Entries);

public enum MarkEntryResult { Created, Updated, Unchanged }

public record MarkedEntry(Guid RecordId, Guid PersonId, PersonType PersonType, AttendanceOutcome Outcome, MarkEntryResult Result);

public enum AttendanceFailureReason
{
    None,
    ValidationFailed, // ERR-901
    NotFound, // ERR-902
    Forbidden, // ERR-102 — a BRL-033 / own-class scope denial decided in the service
    Locked, // ERR-401
    NeedsApproval, // ERR-402
    FutureDate // ERR-403
}

public record AttendanceResult<T>(bool Succeeded, T? Value, AttendanceFailureReason Failure = AttendanceFailureReason.None)
{
    public static AttendanceResult<T> Ok(T value) => new(true, value);
    public static AttendanceResult<T> Fail(AttendanceFailureReason reason) => new(false, default, reason);
}

public record AttendanceQuery(Guid? PersonId, Guid? ClassId, DateOnly? Date);

// API-036's row. `Locked` is derived at read time (04-data-model.md §2.6), never stored.
public record AttendanceView(
    Guid Id, Guid PersonId, PersonType PersonType, DateOnly AttendanceDate, AttendanceOutcome Outcome,
    Guid RecordedByUserId, DateTime RecordedAtUtc, CorrectionStatus CorrectionStatus, bool Locked);

public record CorrectionResult(
    Guid Id, Guid AttendanceRecordId, AttendanceOutcome RequestedOutcome, CorrectionRequestStatus Status,
    AttendanceOutcome RecordOutcome, string Reason, string? ResolutionNote);

// A pending request as the lock sweep sees it: across every institution, with that institution's
// configured time zone so "locked" is computed on the institution's own calendar.
public record PendingCorrection(CorrectionRequest Request, AttendanceRecord Record, string? InstitutionTimeZone);

public interface IAttendanceService
{
    Task<AttendanceResult<IReadOnlyList<RosterEntry>>> GetRosterAsync(Guid classId, DateOnly? date, AttendanceCaller caller, CancellationToken ct); // API-031
    Task<AttendanceResult<IReadOnlyList<MarkedEntry>>> MarkAsync(MarkRequest request, AttendanceCaller caller, CancellationToken ct); // API-032

    Task<AttendanceResult<CorrectionResult>> RequestCorrectionAsync(Guid recordId, string? requestedOutcome, string? reason, AttendanceCaller caller, CancellationToken ct); // API-033
    Task<AttendanceResult<CorrectionResult>> ApproveCorrectionAsync(Guid requestId, AttendanceCaller caller, CancellationToken ct); // API-034
    Task<AttendanceResult<CorrectionResult>> RejectCorrectionAsync(Guid requestId, string? reason, AttendanceCaller caller, CancellationToken ct); // API-034

    Task<AttendanceResult<bool>> RemoveAsync(Guid recordId, AttendanceCaller caller, CancellationToken ct); // API-035
    Task<AttendanceResult<IReadOnlyList<AttendanceView>>> SearchAsync(AttendanceQuery query, AttendanceCaller caller, CancellationToken ct); // API-036
    Task<IReadOnlyList<AttendanceView>> ForOwnStudentAsync(DateOnly? date, AttendanceCaller caller, CancellationToken ct); // API-036 /me
    Task<IReadOnlyList<AttendanceView>> ForLinkedStudentsAsync(DateOnly? date, AttendanceCaller caller, CancellationToken ct); // API-036 /linked

    // Q-206 / TC-410. Auto-rejects every pending request whose record is now locked. Returns the
    // count. Q-216: callable now; the daily Hangfire registration belongs to CT-501 (P5).
    Task<int> RunLockSweepAsync(CancellationToken ct);
}

// Every query runs under the CT-001 tenant filter unless its name says otherwise.
public interface IAttendanceRepository
{
    Task<string?> FindInstitutionTimeZoneAsync(Guid institutionId, CancellationToken ct);
    Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct);
    Task<Guid?> FindTeacherIdForUserAsync(Guid userId, CancellationToken ct);

    // PF-006 pre-condition: "Teacher assigned to the class (or is class teacher)" — class-teacher
    // holding OR any subject mapping for that class.
    Task<bool> TeacherTeachesClassAsync(Guid teacherId, Guid classId, CancellationToken ct);

    Task<IReadOnlyList<Student>> ListActiveStudentsInClassAsync(Guid classId, CancellationToken ct);
    Task<IReadOnlySet<Guid>> FindActiveTeacherIdsAsync(IReadOnlyCollection<Guid> teacherIds, CancellationToken ct);
    Task<IReadOnlyList<AttendanceRecord>> FindRecordsAsync(IReadOnlyCollection<Guid> personIds, DateOnly date, CancellationToken ct);
    Task AddRecordAsync(AttendanceRecord record, CancellationToken ct);

    Task<AttendanceRecord?> FindRecordAsync(Guid recordId, CancellationToken ct);
    Task<Guid?> FindStudentClassIdAsync(Guid studentId, CancellationToken ct); // includes withdrawn students (E1)
    Task<IReadOnlyList<CorrectionRequest>> FindPendingRequestsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct);
    Task<CorrectionRequest?> FindRequestWithRecordAsync(Guid requestId, CancellationToken ct);
    Task AddRequestAsync(CorrectionRequest request, CancellationToken ct);

    Task RemoveRecordAsync(AttendanceRecord record, CancellationToken ct); // hard delete; requests cascade
    // API-036. ClassId filters on the student's CURRENT class placement.
    Task<IReadOnlyList<AttendanceRecord>> SearchRecordsAsync(AttendanceQuery query, CancellationToken ct);
    Task<Guid?> FindStudentIdForUserAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FindLinkedStudentIdsAsync(Guid parentUserId, CancellationToken ct);
    Task<IReadOnlyList<AttendanceRecord>> FindRecordsForPersonsAsync(IReadOnlyCollection<Guid> personIds, DateOnly? date, CancellationToken ct);

    // CROSS-TENANT, deliberately — ADR-0005. The sweep has no caller and no tenant; it reads
    // pending requests for every institution and may only ever move them to AutoRejected.
    Task<IReadOnlyList<PendingCorrection>> ListPendingCorrectionsAcrossInstitutionsAsync(CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
