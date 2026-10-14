using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Configuration;

public record AcademicYearView(Guid Id, string Label, DateOnly StartDate, DateOnly EndDate,
    DateTime? RolloverRequestedAtUtc, DateTime? RolledOverAtUtc, Guid? NextAcademicYearId);

public record RolloverSummary(Guid FromYearId, Guid ToYearId, int Promoted, int HeldBack, int FinalYear);

public enum RolloverFailureReason { None, ValidationFailed, NotFound, Forbidden, AlreadyRolledOver }

public record RolloverResult<T>(bool Succeeded, T? Value, RolloverFailureReason Failure = RolloverFailureReason.None)
{
    public static RolloverResult<T> Ok(T value) => new(true, value);
    public static RolloverResult<T> Fail(RolloverFailureReason reason) => new(false, default, reason);
}

// CT-502: API-059/060 plus the minimal academic-year and class-successor endpoints the rollover
// needs to be operable at all (no contract creates an AcademicYear or sets a successor class).
public interface IRolloverService
{
    Task<RolloverResult<AcademicYearView>> CreateYearAsync(string? label, DateOnly? start, DateOnly? end, ConfigCaller caller, CancellationToken ct);
    Task<IReadOnlyList<AcademicYearView>> ListYearsAsync(CancellationToken ct);
    Task<RolloverResult<Guid?>> SetNextClassAsync(Guid classId, Guid? nextClassId, ConfigCaller caller, CancellationToken ct);
    Task<RolloverResult<AcademicYearView>> RequestRolloverAsync(Guid yearId, ConfigCaller caller, CancellationToken ct); // API-059
    Task<RolloverResult<IReadOnlyList<Guid>>> HoldBackAsync(Guid yearId, IReadOnlyList<Guid>? studentIds, ConfigCaller caller, CancellationToken ct); // API-060

    // PF-012 steps 2–4. Called by the job inside the year's tenant scope (ADR-0007).
    Task<RolloverSummary?> RunAsync(Guid yearId, CancellationToken ct);
}

public interface IRolloverQueue
{
    Task EnqueueAsync(Guid academicYearId, CancellationToken ct);
}

// Tenant-filtered throughout (AcademicYear, Student, Class and RolloverHoldBack are ITenantOwned;
// Enrollment is reached through its tenant-filtered AcademicYear).
public interface IRolloverRepository
{
    Task<AcademicYear?> FindYearAsync(Guid id, CancellationToken ct);
    Task<AcademicYear?> FindYearStartingOnAsync(DateOnly start, CancellationToken ct);
    Task<IReadOnlyList<AcademicYear>> ListYearsAsync(CancellationToken ct);
    Task AddYearAsync(AcademicYear year, CancellationToken ct);

    Task<Class?> FindClassAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, Guid?>> ClassSuccessorsAsync(CancellationToken ct);
    Task<IReadOnlyList<Student>> ActiveStudentsWithClassAsync(CancellationToken ct);
    Task<IReadOnlySet<Guid>> ActiveStudentIdsAsync(IReadOnlyCollection<Guid> candidateIds, CancellationToken ct);
    Task<IReadOnlyList<Student>> StudentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task<IReadOnlyList<Enrollment>> EnrollmentsAsync(Guid yearId, CancellationToken ct);
    Task AddEnrollmentAsync(Enrollment enrollment, CancellationToken ct);

    Task<IReadOnlySet<Guid>> HoldBackStudentIdsAsync(Guid yearId, CancellationToken ct);
    Task AddHoldBackAsync(RolloverHoldBack holdBack, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
