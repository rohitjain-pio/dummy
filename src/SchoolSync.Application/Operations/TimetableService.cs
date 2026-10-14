using System.Globalization;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Operations;

// API-053–055 (CT-403). BRL-053–056, VAL-023, the schedule-conflict decision table, PF-011, Q-225.
public class TimetableService(ITimetableRepository repository, TimeProvider clock) : ITimetableService
{
    private static PeriodView ToView(TimetablePeriod p) =>
        new(p.Id, p.ClassId, p.SubjectId, p.TeacherId, p.Room, p.DayOfWeek, p.TimeSlot, p.IsPublished, p.PublishedAtUtc);

    // Q-225: a 24h "HH:mm" start time — exactly two digits each side, so "9:00" is refused rather
    // than silently treated as a different slot from "09:00".
    public static bool IsValidTimeSlot(string? slot) =>
        slot is { Length: 5 } && TimeOnly.TryParseExact(slot, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private sealed record Validated(Guid ClassId, Guid SubjectId, Guid TeacherId, string Room, DayOfWeekSchool Day, string Slot);

    private async Task<(Validated? Value, TimetableFailureReason Failure)> ValidateAsync(PeriodFields f, CancellationToken ct)
    {
        if (f.ClassId is not { } classId || f.SubjectId is not { } subjectId || f.TeacherId is not { } teacherId
            || string.IsNullOrWhiteSpace(f.Room)
            || !Enum.TryParse<DayOfWeekSchool>(f.DayOfWeek, ignoreCase: false, out var day) || !Enum.IsDefined(day) // VAL-023
            || !IsValidTimeSlot(f.TimeSlot)) // VAL-023 + Q-225
            return (null, TimetableFailureReason.ValidationFailed);

        // PF-011 pre-condition: class, subject and teacher exist (in this institution — tenant-filtered).
        if (!await repository.ClassExistsAsync(classId, ct) || !await repository.SubjectExistsAsync(subjectId, ct)
            || !await repository.ActiveTeacherExistsAsync(teacherId, ct))
            return (null, TimetableFailureReason.ValidationFailed);

        return (new Validated(classId, subjectId, teacherId, f.Room.Trim(), day, f.TimeSlot!), TimetableFailureReason.None);
    }

    // The decision table: all three dimensions checked in ONE pass, every conflict found reported
    // together (TC-608) — never stop at the first. Room is compared case-insensitively, matching the
    // database's default collation that backs the unique index, so the two lines of defence agree.
    private async Task<List<ScheduleConflict>> ConflictsAsync(Validated v, Guid? exceptPeriodId, CancellationToken ct)
    {
        var others = await repository.PeriodsInSlotAsync(v.Day, v.Slot, exceptPeriodId, ct);
        var conflicts = new List<ScheduleConflict>();
        if (others.Any(p => p.TeacherId == v.TeacherId)) conflicts.Add(ScheduleConflict.Teacher); // BRL-053 / ERR-701
        if (others.Any(p => p.ClassId == v.ClassId)) conflicts.Add(ScheduleConflict.Class); // BRL-054 / ERR-702
        if (others.Any(p => string.Equals(p.Room.Trim(), v.Room, StringComparison.OrdinalIgnoreCase))) conflicts.Add(ScheduleConflict.Room); // BRL-055 / ERR-703
        return conflicts;
    }

    public async Task<TimetableResult<PeriodView>> CreateAsync(PeriodFields fields, OperationsCaller caller, CancellationToken ct)
    {
        if (!caller.IsLeadership) return TimetableResult<PeriodView>.Fail(TimetableFailureReason.Forbidden); // BRL-056
        var (v, failure) = await ValidateAsync(fields, ct);
        if (v is null) return TimetableResult<PeriodView>.Fail(failure);

        var conflicts = await ConflictsAsync(v, null, ct);
        if (conflicts.Count > 0) return TimetableResult<PeriodView>.Conflicting(conflicts);

        var period = new TimetablePeriod
        {
            Id = Guid.NewGuid(),
            InstitutionId = caller.InstitutionId,
            ClassId = v.ClassId,
            SubjectId = v.SubjectId,
            TeacherId = v.TeacherId,
            Room = v.Room,
            DayOfWeek = v.Day,
            TimeSlot = v.Slot,
            IsPublished = false
        };
        await repository.AddAsync(period, ct);
        await repository.SaveChangesAsync(ct);
        return TimetableResult<PeriodView>.Ok(ToView(period));
    }

    public async Task<TimetableResult<PeriodView>> UpdateAsync(Guid id, PeriodFields fields, OperationsCaller caller, CancellationToken ct)
    {
        if (!caller.IsLeadership) return TimetableResult<PeriodView>.Fail(TimetableFailureReason.Forbidden);
        var period = await repository.FindAsync(id, ct);
        if (period is null) return TimetableResult<PeriodView>.Fail(TimetableFailureReason.NotFound);
        var (v, failure) = await ValidateAsync(fields, ct);
        if (v is null) return TimetableResult<PeriodView>.Fail(failure);

        var conflicts = await ConflictsAsync(v, exceptPeriodId: id, ct);
        if (conflicts.Count > 0) return TimetableResult<PeriodView>.Conflicting(conflicts);

        period.ClassId = v.ClassId;
        period.SubjectId = v.SubjectId;
        period.TeacherId = v.TeacherId;
        period.Room = v.Room;
        period.DayOfWeek = v.Day;
        period.TimeSlot = v.Slot;
        await repository.SaveChangesAsync(ct); // publish state is unchanged by an edit
        return TimetableResult<PeriodView>.Ok(ToView(period));
    }

    public async Task<TimetableResult<PeriodView>> PublishAsync(Guid id, OperationsCaller caller, CancellationToken ct)
    {
        if (!caller.IsLeadership) return TimetableResult<PeriodView>.Fail(TimetableFailureReason.Forbidden);
        var period = await repository.FindAsync(id, ct);
        if (period is null) return TimetableResult<PeriodView>.Fail(TimetableFailureReason.NotFound);

        if (!period.IsPublished) // publishing twice is a no-op
        {
            period.IsPublished = true;
            period.PublishedAtUtc = clock.GetUtcNow().UtcDateTime;
            await repository.SaveChangesAsync(ct);
        }
        return TimetableResult<PeriodView>.Ok(ToView(period));
    }

    // BR-056 / Q-225: leadership sees drafts; everyone else sees published periods only.
    public async Task<IReadOnlyList<PeriodView>> ListAsync(Guid? classId, Guid? teacherId, OperationsCaller caller, CancellationToken ct) =>
        (await repository.ListAsync(classId, teacherId, publishedOnly: !caller.IsLeadership, ct)).Select(ToView).ToList();
}
