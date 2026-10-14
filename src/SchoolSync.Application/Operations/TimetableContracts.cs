using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Operations;

public record PeriodFields(Guid? ClassId, Guid? SubjectId, Guid? TeacherId, string? Room, string? DayOfWeek, string? TimeSlot);

public record PeriodView(
    Guid Id, Guid ClassId, Guid SubjectId, Guid TeacherId, string Room, DayOfWeekSchool DayOfWeek, string TimeSlot,
    bool IsPublished, DateTime? PublishedAtUtc);

public enum ScheduleConflict { Teacher, Class, Room } // ERR-701 / ERR-702 / ERR-703

public enum TimetableFailureReason { None, ValidationFailed, NotFound, Forbidden, Conflict }

public record TimetableResult<T>(bool Succeeded, T? Value, TimetableFailureReason Failure = TimetableFailureReason.None,
    IReadOnlyList<ScheduleConflict>? Conflicts = null)
{
    public static TimetableResult<T> Ok(T value) => new(true, value);
    public static TimetableResult<T> Fail(TimetableFailureReason reason) => new(false, default, reason);
    public static TimetableResult<T> Conflicting(IReadOnlyList<ScheduleConflict> conflicts) => new(false, default, TimetableFailureReason.Conflict, conflicts);
}

// API-053–055 (CT-403).
public interface ITimetableService
{
    Task<TimetableResult<PeriodView>> CreateAsync(PeriodFields fields, OperationsCaller caller, CancellationToken ct); // API-053 POST
    Task<TimetableResult<PeriodView>> UpdateAsync(Guid id, PeriodFields fields, OperationsCaller caller, CancellationToken ct); // API-053 PUT
    Task<TimetableResult<PeriodView>> PublishAsync(Guid id, OperationsCaller caller, CancellationToken ct); // API-054
    Task<IReadOnlyList<PeriodView>> ListAsync(Guid? classId, Guid? teacherId, OperationsCaller caller, CancellationToken ct); // API-055
}

public interface ITimetableRepository
{
    Task<bool> ClassExistsAsync(Guid id, CancellationToken ct);
    Task<bool> SubjectExistsAsync(Guid id, CancellationToken ct);
    Task<bool> ActiveTeacherExistsAsync(Guid id, CancellationToken ct);

    // Every other period in the same (day, slot) — the conflict-check key (VAL-023) — excluding
    // the period being edited, so a save never conflicts with itself.
    Task<IReadOnlyList<TimetablePeriod>> PeriodsInSlotAsync(DayOfWeekSchool day, string timeSlot, Guid? exceptPeriodId, CancellationToken ct);

    Task<TimetablePeriod?> FindAsync(Guid id, CancellationToken ct);
    Task AddAsync(TimetablePeriod period, CancellationToken ct);
    Task<IReadOnlyList<TimetablePeriod>> ListAsync(Guid? classId, Guid? teacherId, bool publishedOnly, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
