using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Operations;

public record ReportRequest(string? ReportType, DateOnly? From, DateOnly? To, Guid? ScopeClassId, string? Format);

public record ReportRunView(
    Guid Id, ReportType ReportType, ReportFormat Format, DateOnly From, DateOnly To, Guid? ScopeClassId,
    ReportRunStatus Status, Guid RequestedByUserId, DateTime RequestedAtUtc, DateTime? GeneratedAtUtc, string? FailureReason);

public record ReportFile(string FileName, string ContentType, Stream Content);

public enum ReportFailureReason { None, ValidationFailed, NotFound, Forbidden, NotReady }

public record ReportResult<T>(bool Succeeded, T? Value, ReportFailureReason Failure = ReportFailureReason.None)
{
    public static ReportResult<T> Ok(T value) => new(true, value);
    public static ReportResult<T> Fail(ReportFailureReason reason) => new(false, default, reason);
}

// API-050–052 (CT-402).
public interface IReportService
{
    Task<ReportResult<ReportRunView>> RequestAsync(ReportRequest request, OperationsCaller caller, CancellationToken ct); // API-050
    Task<IReadOnlyList<ReportRunView>> ListAsync(OperationsCaller caller, CancellationToken ct); // API-051
    Task<ReportResult<ReportRunView>> FindAsync(Guid id, OperationsCaller caller, CancellationToken ct); // API-051
    Task<ReportResult<ReportFile>> DownloadAsync(Guid id, OperationsCaller caller, CancellationToken ct); // API-052
}

// A1: POST returns 202 and the work happens elsewhere. Hangfire in the app (Q-222); a synchronous
// runner in HTTP tests, because Hangfire's static JobStorage cannot be isolated across parallel hosts.
public interface IReportJobQueue
{
    Task EnqueueAsync(Guid reportRunId, CancellationToken ct);
}

// D11 / Q-222: Azure Blob Storage when configured, otherwise local disk (dev/test only).
public interface IReportFileStore
{
    Task<string> SaveAsync(Guid reportRunId, string fileName, byte[] content, CancellationToken ct);
    Task<Stream> OpenAsync(string uri, CancellationToken ct);
}

public interface IReportRunRepository
{
    Task<bool> TeacherTeachesClassAsync(Guid userId, Guid classId, CancellationToken ct);
    Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct);
    Task AddAsync(ReportRun run, CancellationToken ct);
    Task<ReportRun?> FindAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ReportRun>> ListAsync(Guid? onlyRequestedBy, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

// ---- Generation (runs inside the job) ----

public record ClassLabel(Guid Id, string Name, string Section);
public record StudentLabel(Guid Id, string Name, string SrNumber, Guid? ClassId);
public record SubjectClasses(Guid SubjectId, string SubjectName, IReadOnlyList<Guid> ClassIds);
public record TeacherLabel(Guid TeacherId, string Name, Guid? UserAccountId, IReadOnlyList<Guid> ClassIds);
public record AttendanceRow(Guid PersonId, DateOnly Date, AttendanceOutcome Outcome, Guid RecordedByUserId, DateTime RecordedAtUtc);

// Everything a report may read. The performance metrics (FR-057/058) are computed ONLY from
// ListStudentAttendanceAsync; the other members supply names and class placement for the rows.
// There is no member that could return homework, participation, satisfaction or completion data,
// because no such data exists in the schema (TC-601/602/609).
public interface IReportDataSource
{
    Task<string?> InstitutionTimeZoneAsync(CancellationToken ct);
    Task<IReadOnlyList<ClassLabel>> ClassesAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct);
    Task<IReadOnlyList<StudentLabel>> StudentsAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct);
    Task<IReadOnlyList<SubjectClasses>> SubjectsAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct);
    Task<IReadOnlyList<TeacherLabel>> TeachersAsync(Guid? onlyTeacherUserId, Guid? onlyClassId, CancellationToken ct);
    Task<IReadOnlyList<AttendanceRow>> StudentAttendanceAsync(IReadOnlyCollection<Guid> studentIds, DateOnly from, DateOnly to, CancellationToken ct);
}
