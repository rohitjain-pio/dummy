using System.Globalization;
using System.Text.RegularExpressions;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Configuration;

// CT-502. PF-012, BRL-060/067, VAL-026, Q-229. Enrollment is APPEND-ONLY (BR-060, D14): this class
// only ever adds Enrollment rows — there is no code path here that edits or removes one.
public partial class RolloverService(IRolloverRepository repository, IRolloverQueue queue, TimeProvider clock) : IRolloverService
{
    private static AcademicYearView ToView(AcademicYear y) =>
        new(y.Id, y.Label, y.StartDate, y.EndDate, y.RolloverRequestedAtUtc, y.RolledOverAtUtc, y.NextAcademicYearId);

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<RolloverResult<AcademicYearView>> CreateYearAsync(string? label, DateOnly? start, DateOnly? end, ConfigCaller caller, CancellationToken ct)
    {
        if (!caller.IsLeadership) return RolloverResult<AcademicYearView>.Fail(RolloverFailureReason.Forbidden);
        if (string.IsNullOrWhiteSpace(label) || start is not { } s || end is not { } e || e <= s)
            return RolloverResult<AcademicYearView>.Fail(RolloverFailureReason.ValidationFailed);

        var year = new AcademicYear { Id = Guid.NewGuid(), InstitutionId = caller.InstitutionId, Label = label.Trim(), StartDate = s, EndDate = e };
        await repository.AddYearAsync(year, ct);
        await repository.SaveChangesAsync(ct);
        return RolloverResult<AcademicYearView>.Ok(ToView(year));
    }

    public async Task<IReadOnlyList<AcademicYearView>> ListYearsAsync(CancellationToken ct) =>
        (await repository.ListYearsAsync(ct)).Select(ToView).ToList();

    public async Task<RolloverResult<Guid?>> SetNextClassAsync(Guid classId, Guid? nextClassId, ConfigCaller caller, CancellationToken ct)
    {
        if (!caller.IsLeadership) return RolloverResult<Guid?>.Fail(RolloverFailureReason.Forbidden);
        var @class = await repository.FindClassAsync(classId, ct);
        if (@class is null) return RolloverResult<Guid?>.Fail(RolloverFailureReason.NotFound);
        if (nextClassId is { } next && (next == classId || await repository.FindClassAsync(next, ct) is null))
            return RolloverResult<Guid?>.Fail(RolloverFailureReason.ValidationFailed); // must be another class in this institution

        @class.NextClassId = nextClassId;
        await repository.SaveChangesAsync(ct);
        return RolloverResult<Guid?>.Ok(nextClassId);
    }

    public async Task<RolloverResult<AcademicYearView>> RequestRolloverAsync(Guid yearId, ConfigCaller caller, CancellationToken ct)
    {
        if (!caller.IsLeadership) return RolloverResult<AcademicYearView>.Fail(RolloverFailureReason.Forbidden);
        var year = await repository.FindYearAsync(yearId, ct);
        if (year is null) return RolloverResult<AcademicYearView>.Fail(RolloverFailureReason.NotFound);
        if (year.RolledOverAtUtc is not null || year.RolloverRequestedAtUtc is not null)
            return RolloverResult<AcademicYearView>.Fail(RolloverFailureReason.AlreadyRolledOver); // a year rolls over exactly once

        year.RolloverRequestedAtUtc = Now;
        await repository.SaveChangesAsync(ct);
        await queue.EnqueueAsync(yearId, ct); // 202: async (A1 pattern, PF-012)
        return RolloverResult<AcademicYearView>.Ok(ToView(year));
    }

    public async Task<RolloverResult<IReadOnlyList<Guid>>> HoldBackAsync(Guid yearId, IReadOnlyList<Guid>? studentIds, ConfigCaller caller, CancellationToken ct)
    {
        static RolloverResult<IReadOnlyList<Guid>> Fail(RolloverFailureReason r) => RolloverResult<IReadOnlyList<Guid>>.Fail(r);

        if (!caller.IsLeadership) return Fail(RolloverFailureReason.Forbidden);
        var year = await repository.FindYearAsync(yearId, ct);
        if (year is null) return Fail(RolloverFailureReason.NotFound);
        if (studentIds is null || studentIds.Count == 0) return Fail(RolloverFailureReason.ValidationFailed);

        // VAL-026 / TC-703: every id must be an ACTIVE student of this institution — all or nothing.
        // A student of another institution is simply not visible under the tenant filter.
        var distinct = studentIds.Distinct().ToList();
        if ((await repository.ActiveStudentIdsAsync(distinct, ct)).Count != distinct.Count) return Fail(RolloverFailureReason.ValidationFailed);

        var already = await repository.HoldBackStudentIdsAsync(yearId, ct);
        foreach (var id in distinct.Where(id => !already.Contains(id)))
            await repository.AddHoldBackAsync(new RolloverHoldBack { AcademicYearId = yearId, StudentId = id, InstitutionId = year.InstitutionId, CreatedByUserId = caller.UserId, CreatedAtUtc = Now }, ct);

        // FR-067 "before or after": if the year has already rolled over, the hold-back is applied
        // now — by APPENDING a new next-year enrolment in the student's prior class, never by
        // editing the promotion row the rollover wrote.
        if (year.RolledOverAtUtc is not null && year.NextAcademicYearId is { } nextYearId)
            await ApplyHoldBacksAfterRolloverAsync(yearId, nextYearId, distinct, ct);

        await repository.SaveChangesAsync(ct);
        return RolloverResult<IReadOnlyList<Guid>>.Ok(distinct);
    }

    private async Task ApplyHoldBacksAfterRolloverAsync(Guid fromYearId, Guid toYearId, IReadOnlyList<Guid> studentIds, CancellationToken ct)
    {
        var fromYear = await repository.EnrollmentsAsync(fromYearId, ct);
        var students = (await repository.StudentsAsync(studentIds, ct)).ToDictionary(s => s.Id);

        foreach (var id in studentIds)
        {
            // The class the student ended the closed year in: the latest of that year's rows (a
            // mid-year move leaves several, written at different times; the rollover's own snapshot
            // is only written when none existed).
            var priorClass = fromYear.Where(e => e.StudentId == id).OrderByDescending(e => e.CreatedAtUtc).FirstOrDefault()?.ClassId;
            if (priorClass is null) continue; // not part of that year's roster

            // "Already held back" is judged by the student's CURRENT placement (Student.ClassId), not
            // by ordering Enrollment rows by CreatedAtUtc — rows written in the same instant tie, and a
            // tie picked the promotion row and appended a duplicate correction (found by test).
            if (students[id].ClassId == priorClass) continue;

            await repository.AddEnrollmentAsync(new Enrollment { Id = Guid.NewGuid(), AcademicYearId = toYearId, StudentId = id, ClassId = priorClass.Value, CreatedAtUtc = Now }, ct);
            students[id].ClassId = priorClass; // current placement
        }
    }

    public async Task<RolloverSummary?> RunAsync(Guid yearId, CancellationToken ct)
    {
        var year = await repository.FindYearAsync(yearId, ct);
        if (year is null || year.RolledOverAtUtc is not null) return null; // idempotent: never twice

        var nextStart = year.EndDate.AddDays(1);
        var next = await repository.FindYearStartingOnAsync(nextStart, ct);
        if (next is null)
        {
            next = new AcademicYear
            {
                Id = Guid.NewGuid(),
                InstitutionId = year.InstitutionId,
                Label = NextLabel(year.Label),
                StartDate = nextStart,
                EndDate = nextStart.AddDays(year.EndDate.DayNumber - year.StartDate.DayNumber)
            };
            await repository.AddYearAsync(next, ct);
        }

        var successors = await repository.ClassSuccessorsAsync(ct);
        var heldBack = await repository.HoldBackStudentIdsAsync(yearId, ct);
        var existing = (await repository.EnrollmentsAsync(yearId, ct)).Select(e => e.StudentId).ToHashSet();
        int promoted = 0, held = 0, finalYear = 0;

        foreach (var student in await repository.ActiveStudentsWithClassAsync(ct))
        {
            var currentClass = student.ClassId!.Value;

            // History first: a student enrolled before any Enrollment row existed gets this year's
            // placement recorded as it stands, so the year being closed stays queryable (BR-060).
            if (!existing.Contains(student.Id))
                await repository.AddEnrollmentAsync(new Enrollment { Id = Guid.NewGuid(), AcademicYearId = yearId, StudentId = student.Id, ClassId = currentClass, CreatedAtUtc = Now }, ct);

            Guid? target;
            if (heldBack.Contains(student.Id)) { target = currentClass; held++; } // TC-702: same class level again
            else if (successors.GetValueOrDefault(currentClass) is { } nextClass) { target = nextClass; promoted++; }
            else { finalYear++; continue; } // Q-229: no successor — no new-year enrolment, student left active

            await repository.AddEnrollmentAsync(new Enrollment { Id = Guid.NewGuid(), AcademicYearId = next.Id, StudentId = student.Id, ClassId = target.Value, CreatedAtUtc = Now }, ct);
            student.ClassId = target; // current placement moves; the history is in the Enrollment rows
        }

        year.RolledOverAtUtc = Now;
        year.RolloverRequestedAtUtc ??= Now;
        year.NextAcademicYearId = next.Id;
        await repository.SaveChangesAsync(ct); // one transaction for the whole roster
        return new RolloverSummary(yearId, next.Id, promoted, held, finalYear);
    }

    [GeneratedRegex(@"^(\d{4})-(\d{2}|\d{4})$")]
    private static partial Regex YearLabel();

    // "2026-27" -> "2027-28"; "2026-2027" -> "2027-2028"; anything else -> "<label> (next)".
    public static string NextLabel(string label)
    {
        var m = YearLabel().Match(label.Trim());
        if (!m.Success) return $"{label.Trim()} (next)";
        var first = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) + 1;
        var second = m.Groups[2].Value.Length == 2 ? ((first + 1) % 100).ToString("00", CultureInfo.InvariantCulture) : (first + 1).ToString(CultureInfo.InvariantCulture);
        return $"{first}-{second}";
    }
}
