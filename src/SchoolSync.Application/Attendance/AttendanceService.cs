using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Attendance;

// API-031/032 (CT-201). The correction/lock state machine (07-business-logic/attendance.md) is
// enforced here for the bulk path; CT-202 adds the explicit correction-request path on top.
public class AttendanceService(IAttendanceRepository repository, TimeProvider clock) : IAttendanceService
{
    private async Task<DateOnly> InstitutionTodayAsync(Guid institutionId, CancellationToken ct)
    {
        var zone = AttendanceCalendar.ResolveTimeZone(
            await repository.FindInstitutionTimeZoneAsync(institutionId, ct), out _);
        return AttendanceCalendar.LocalToday(clock.GetUtcNow(), zone);
    }

    // A Teacher may act only on a class they teach; leadership on any class in their institution.
    private async Task<bool> MayActOnClassAsync(Guid classId, AttendanceCaller caller, CancellationToken ct)
    {
        if (caller.IsLeadership) return true;
        if (caller.Role != UserRole.Teacher) return false;

        var teacherId = await repository.FindTeacherIdForUserAsync(caller.UserId, ct);
        return teacherId is not null && await repository.TeacherTeachesClassAsync(teacherId.Value, classId, ct);
    }

    public async Task<AttendanceResult<IReadOnlyList<RosterEntry>>> GetRosterAsync(
        Guid classId, DateOnly? date, AttendanceCaller caller, CancellationToken ct)
    {
        if (!await repository.ClassExistsAsync(classId, ct))
            return AttendanceResult<IReadOnlyList<RosterEntry>>.Fail(AttendanceFailureReason.NotFound);
        if (!await MayActOnClassAsync(classId, caller, ct))
            return AttendanceResult<IReadOnlyList<RosterEntry>>.Fail(AttendanceFailureReason.Forbidden);

        var day = date ?? await InstitutionTodayAsync(caller.InstitutionId, ct);
        var students = await repository.ListActiveStudentsInClassAsync(classId, ct);
        var existing = (await repository.FindRecordsAsync(students.Select(s => s.Id).ToList(), day, ct))
            .ToDictionary(r => r.PersonId);

        // BRL-034: a student with no row is NotYetMarked -> null. Never "Absent".
        IReadOnlyList<RosterEntry> roster = students
            .Select(s => new RosterEntry(s.Id, s.Name, existing.TryGetValue(s.Id, out var r) ? r.Outcome : null))
            .ToList();
        return AttendanceResult<IReadOnlyList<RosterEntry>>.Ok(roster);
    }

    public async Task<AttendanceResult<IReadOnlyList<MarkedEntry>>> MarkAsync(
        MarkRequest request, AttendanceCaller caller, CancellationToken ct)
    {
        static AttendanceResult<IReadOnlyList<MarkedEntry>> Fail(AttendanceFailureReason r) =>
            AttendanceResult<IReadOnlyList<MarkedEntry>>.Fail(r);

        // VAL-013 and the entry shape.
        if (request.Entries.Count == 0) return Fail(AttendanceFailureReason.ValidationFailed);
        var parsed = new List<(Guid PersonId, PersonType Type, AttendanceOutcome Outcome)>();
        foreach (var e in request.Entries)
        {
            if (!Enum.TryParse<PersonType>(e.PersonType, ignoreCase: false, out var type) || !Enum.IsDefined(type)
                || !Enum.TryParse<AttendanceOutcome>(e.Outcome, ignoreCase: false, out var outcome) || !Enum.IsDefined(outcome))
                return Fail(AttendanceFailureReason.ValidationFailed);
            parsed.Add((e.PersonId, type, outcome));
        }
        // BRL-031: one outcome per person per date — the same person twice in one request is ambiguous.
        if (parsed.Select(p => p.PersonId).Distinct().Count() != parsed.Count)
            return Fail(AttendanceFailureReason.ValidationFailed);

        // VAL-014 then CFG-004, in the institution's own calendar.
        var today = await InstitutionTodayAsync(caller.InstitutionId, ct);
        var daysSince = AttendanceCalendar.DaysSince(request.Date, today);
        if (daysSince < 0) return Fail(AttendanceFailureReason.FutureDate);
        if (AttendanceCalendar.IsLocked(daysSince)) return Fail(AttendanceFailureReason.Locked);

        // BRL-033: staff attendance is leadership only.
        var studentIds = parsed.Where(p => p.Type == PersonType.Student).Select(p => p.PersonId).ToList();
        var teacherIds = parsed.Where(p => p.Type == PersonType.Teacher).Select(p => p.PersonId).ToList();
        if (teacherIds.Count > 0 && !caller.IsLeadership) return Fail(AttendanceFailureReason.Forbidden);

        if (studentIds.Count > 0)
        {
            // API-032 marks a class roster; student entries need the class they are marked against.
            if (request.ClassId is not { } classId) return Fail(AttendanceFailureReason.ValidationFailed);
            if (!await repository.ClassExistsAsync(classId, ct)) return Fail(AttendanceFailureReason.NotFound);
            if (!await MayActOnClassAsync(classId, caller, ct)) return Fail(AttendanceFailureReason.Forbidden);

            // VAL-015: an active student of this institution, placed in this class.
            var roster = (await repository.ListActiveStudentsInClassAsync(classId, ct)).Select(s => s.Id).ToHashSet();
            if (studentIds.Any(id => !roster.Contains(id))) return Fail(AttendanceFailureReason.ValidationFailed);
        }

        if (teacherIds.Count > 0)
        {
            var active = await repository.FindActiveTeacherIdsAsync(teacherIds, ct); // VAL-015
            if (teacherIds.Any(id => !active.Contains(id))) return Fail(AttendanceFailureReason.ValidationFailed);
        }

        // Decision table, day 1-30: a Teacher cannot write past-day data directly — that is what
        // the correction request (API-033) is for. Leadership applies immediately (Q-205).
        if (daysSince >= 1 && !caller.IsLeadership) return Fail(AttendanceFailureReason.NeedsApproval);

        var existing = (await repository.FindRecordsAsync(parsed.Select(p => p.PersonId).ToList(), request.Date, ct))
            .ToDictionary(r => r.PersonId);

        // SPEC-FR-033: same-day direct correction is the ORIGINAL recording teacher's. A Teacher
        // may not silently overwrite an outcome someone else recorded today.
        if (!caller.IsLeadership && parsed.Any(p =>
                existing.TryGetValue(p.PersonId, out var r) && r.Outcome != p.Outcome && r.RecordedByUserId != caller.UserId))
            return Fail(AttendanceFailureReason.Forbidden);

        var nowUtc = clock.GetUtcNow().UtcDateTime;

        // CT-202: a leadership direct write replaces the outcome outright (Q-205), so any request
        // still pending on a record whose outcome it changes is closed as Superseded — otherwise a
        // later approval would silently overwrite leadership's correction with a stale value.
        var changedRecordIds = parsed
            .Where(p => existing.TryGetValue(p.PersonId, out var r) && r.Outcome != p.Outcome)
            .Select(p => existing[p.PersonId].Id).ToList();
        if (changedRecordIds.Count > 0)
        {
            foreach (var pending in await repository.FindPendingRequestsAsync(changedRecordIds, ct))
            {
                pending.Status = CorrectionRequestStatus.Superseded;
                pending.ResolvedByUserId = caller.UserId;
                pending.ResolvedAtUtc = nowUtc;
                pending.ResolutionNote = "Superseded by a direct leadership correction.";
                existing.Values.Single(r => r.Id == pending.AttendanceRecordId).CorrectionStatus = CorrectionStatus.None;
            }
        }

        var results = new List<MarkedEntry>(parsed.Count);
        foreach (var (personId, type, outcome) in parsed)
        {
            if (existing.TryGetValue(personId, out var record))
            {
                // BRL-031 / D10: update in place — never a second row for (person, date).
                // RecordedByUserId stays the original recorder (SPEC-FR-033's check depends on it);
                // who corrected it is carried by the audit row (BRL-035).
                var changed = record.Outcome != outcome;
                record.Outcome = outcome;
                results.Add(new MarkedEntry(record.Id, personId, record.PersonType, outcome,
                    changed ? MarkEntryResult.Updated : MarkEntryResult.Unchanged));
                continue;
            }

            var created = new AttendanceRecord
            {
                Id = Guid.NewGuid(),
                InstitutionId = caller.InstitutionId,
                PersonId = personId,
                PersonType = type,
                AttendanceDate = request.Date,
                Outcome = outcome,
                RecordedByUserId = caller.UserId,
                RecordedAtUtc = nowUtc,
                CorrectionStatus = CorrectionStatus.None
            };
            await repository.AddRecordAsync(created, ct);
            results.Add(new MarkedEntry(created.Id, personId, type, outcome, MarkEntryResult.Created));
        }

        // One SaveChanges = one transaction for the whole roster (NFR-022), with the CT-003
        // interceptor writing one audit row per changed record inside it (NFR-028).
        await repository.SaveChangesAsync(ct);
        return AttendanceResult<IReadOnlyList<MarkedEntry>>.Ok(results);
    }

    // ---- CT-202: the correction/approval state machine (07-business-logic/attendance.md) ----

    private static CorrectionResult ToResult(CorrectionRequest r, AttendanceRecord record) =>
        new(r.Id, r.AttendanceRecordId, r.RequestedOutcome, r.Status, record.Outcome, r.Reason, r.ResolutionNote);

    // Who may raise a correction on this record: Student records — leadership, or a Teacher of the
    // student's current class; Teacher (staff) records — leadership only (BRL-033).
    private async Task<bool> MayCorrectAsync(AttendanceRecord record, AttendanceCaller caller, CancellationToken ct)
    {
        if (caller.IsLeadership) return true;
        if (caller.Role != UserRole.Teacher || record.PersonType != PersonType.Student) return false;

        var classId = await repository.FindStudentClassIdAsync(record.PersonId, ct);
        return classId is not null && await MayActOnClassAsync(classId.Value, caller, ct);
    }

    public async Task<AttendanceResult<CorrectionResult>> RequestCorrectionAsync(
        Guid recordId, string? requestedOutcome, string? reason, AttendanceCaller caller, CancellationToken ct)
    {
        static AttendanceResult<CorrectionResult> Fail(AttendanceFailureReason r) => AttendanceResult<CorrectionResult>.Fail(r);

        var record = await repository.FindRecordAsync(recordId, ct);
        if (record is null) return Fail(AttendanceFailureReason.NotFound);
        if (!await MayCorrectAsync(record, caller, ct)) return Fail(AttendanceFailureReason.Forbidden);

        // TC-408: the lock is checked before anything is created — a locked record yields no request.
        var daysSince = AttendanceCalendar.DaysSince(record.AttendanceDate, await InstitutionTodayAsync(caller.InstitutionId, ct));
        if (AttendanceCalendar.IsLocked(daysSince)) return Fail(AttendanceFailureReason.Locked);

        if (!Enum.TryParse<AttendanceOutcome>(requestedOutcome, ignoreCase: false, out var outcome) || !Enum.IsDefined(outcome)
            || string.IsNullOrWhiteSpace(reason))
            return Fail(AttendanceFailureReason.ValidationFailed); // VAL-013; API-033's reason is required

        // E3: only one CorrectionPending request per record at a time.
        if ((await repository.FindPendingRequestsAsync([record.Id], ct)).Count > 0)
            return Fail(AttendanceFailureReason.ValidationFailed);

        // Decision table. Day 0: the original recorder (or leadership) corrects directly. Day 1-30:
        // leadership applies immediately (Q-205); a Teacher's request waits for approval.
        var applyDirectly = caller.IsLeadership || (daysSince == 0 && record.RecordedByUserId == caller.UserId);
        if (!applyDirectly && daysSince == 0) return Fail(AttendanceFailureReason.Forbidden); // SPEC-FR-033

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var request = new CorrectionRequest
        {
            Id = Guid.NewGuid(),
            InstitutionId = record.InstitutionId,
            AttendanceRecordId = record.Id,
            RequestedOutcome = outcome,
            Reason = reason.Trim(),
            RequestedByUserId = caller.UserId,
            RequestedAtUtc = nowUtc,
            Status = applyDirectly ? CorrectionRequestStatus.AppliedDirectly : CorrectionRequestStatus.Pending
        };

        if (applyDirectly)
        {
            request.ResolvedByUserId = caller.UserId;
            request.ResolvedAtUtc = nowUtc;
            record.Outcome = outcome;
            record.CorrectionStatus = CorrectionStatus.None;
        }
        else
        {
            record.CorrectionStatus = CorrectionStatus.CorrectionPending; // the record is not changed yet
        }

        await repository.AddRequestAsync(request, ct);
        await repository.SaveChangesAsync(ct);
        return AttendanceResult<CorrectionResult>.Ok(ToResult(request, record));
    }

    public Task<AttendanceResult<CorrectionResult>> ApproveCorrectionAsync(Guid requestId, AttendanceCaller caller, CancellationToken ct) =>
        ResolveAsync(requestId, approve: true, note: null, caller, ct);

    public Task<AttendanceResult<CorrectionResult>> RejectCorrectionAsync(Guid requestId, string? reason, AttendanceCaller caller, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(reason) // PF-007 step 4: "the request closes with a reason"
            ? Task.FromResult(AttendanceResult<CorrectionResult>.Fail(AttendanceFailureReason.ValidationFailed))
            : ResolveAsync(requestId, approve: false, note: reason.Trim(), caller, ct);

    private async Task<AttendanceResult<CorrectionResult>> ResolveAsync(
        Guid requestId, bool approve, string? note, AttendanceCaller caller, CancellationToken ct)
    {
        static AttendanceResult<CorrectionResult> Fail(AttendanceFailureReason r) => AttendanceResult<CorrectionResult>.Fail(r);

        if (!caller.IsLeadership) return Fail(AttendanceFailureReason.Forbidden); // Q-204 approver roles
        var request = await repository.FindRequestWithRecordAsync(requestId, ct);
        if (request is null) return Fail(AttendanceFailureReason.NotFound);
        if (request.Status != CorrectionRequestStatus.Pending) return Fail(AttendanceFailureReason.ValidationFailed);

        var record = request.AttendanceRecord;
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        // Q-206 + Q-212 "both": the lock is re-checked at the moment of action, whenever the sweep
        // last ran. A request found past the lock is auto-rejected here and the action refused.
        var daysSince = AttendanceCalendar.DaysSince(record.AttendanceDate, await InstitutionTodayAsync(caller.InstitutionId, ct));
        if (AttendanceCalendar.IsLocked(daysSince))
        {
            AutoReject(request, record, nowUtc);
            await repository.SaveChangesAsync(ct);
            return Fail(AttendanceFailureReason.Locked);
        }

        request.Status = approve ? CorrectionRequestStatus.Approved : CorrectionRequestStatus.Rejected;
        request.ResolvedByUserId = caller.UserId;
        request.ResolvedAtUtc = nowUtc;
        request.ResolutionNote = note;
        if (approve) record.Outcome = request.RequestedOutcome;
        record.CorrectionStatus = CorrectionStatus.None;

        await repository.SaveChangesAsync(ct);
        return AttendanceResult<CorrectionResult>.Ok(ToResult(request, record));
    }

    private static void AutoReject(CorrectionRequest request, AttendanceRecord record, DateTime nowUtc)
    {
        request.Status = CorrectionRequestStatus.AutoRejected;
        request.ResolvedByUserId = null;
        request.ResolvedAtUtc = nowUtc;
        request.ResolutionNote = "Automatically rejected: the attendance record locked before this request was resolved.";
        record.CorrectionStatus = CorrectionStatus.None;
    }

    // ---- CT-203: removal (API-035) and retrieval (API-036) ----

    public async Task<AttendanceResult<bool>> RemoveAsync(Guid recordId, AttendanceCaller caller, CancellationToken ct)
    {
        if (!caller.IsLeadership) return AttendanceResult<bool>.Fail(AttendanceFailureReason.Forbidden); // SPEC-FR-036
        var record = await repository.FindRecordAsync(recordId, ct);
        if (record is null) return AttendanceResult<bool>.Fail(AttendanceFailureReason.NotFound);

        // "Locked (terminal — no further mutation by anyone)": removal is a mutation.
        var daysSince = AttendanceCalendar.DaysSince(record.AttendanceDate, await InstitutionTodayAsync(caller.InstitutionId, ct));
        if (AttendanceCalendar.IsLocked(daysSince)) return AttendanceResult<bool>.Fail(AttendanceFailureReason.Locked);

        // Hard delete; the CT-003 interceptor writes the Delete audit row with the before-image, and
        // the record's correction requests cascade with it (each also audited).
        await repository.RemoveRecordAsync(record, ct);
        await repository.SaveChangesAsync(ct);
        return AttendanceResult<bool>.Ok(true);
    }

    private async Task<IReadOnlyList<AttendanceView>> ToViewsAsync(
        IReadOnlyList<AttendanceRecord> records, Guid institutionId, CancellationToken ct)
    {
        var today = await InstitutionTodayAsync(institutionId, ct);
        return records.Select(r => new AttendanceView(
            r.Id, r.PersonId, r.PersonType, r.AttendanceDate, r.Outcome, r.RecordedByUserId, r.RecordedAtUtc,
            r.CorrectionStatus ?? CorrectionStatus.None,
            AttendanceCalendar.IsLocked(AttendanceCalendar.DaysSince(r.AttendanceDate, today)))).ToList();
    }

    public async Task<AttendanceResult<IReadOnlyList<AttendanceView>>> SearchAsync(
        AttendanceQuery query, AttendanceCaller caller, CancellationToken ct)
    {
        // Leadership: the whole institution (FR-037's "institutional list"). Teacher: "own class"
        // only — so a Teacher must name a class they teach, and the class filter confines every row
        // to that class's students (staff records never appear to a Teacher).
        if (!caller.IsLeadership)
        {
            if (caller.Role != UserRole.Teacher || query.ClassId is not { } classId || !await MayActOnClassAsync(classId, caller, ct))
                return AttendanceResult<IReadOnlyList<AttendanceView>>.Fail(AttendanceFailureReason.Forbidden);
        }

        var records = await repository.SearchRecordsAsync(query, ct);
        return AttendanceResult<IReadOnlyList<AttendanceView>>.Ok(await ToViewsAsync(records, caller.InstitutionId, ct));
    }

    public async Task<IReadOnlyList<AttendanceView>> ForOwnStudentAsync(DateOnly? date, AttendanceCaller caller, CancellationToken ct)
    {
        // FR-074 pattern (API-019): a Student sees only the record linked to their own account.
        var studentId = await repository.FindStudentIdForUserAsync(caller.UserId, ct);
        if (studentId is null) return [];
        return await ToViewsAsync(await repository.FindRecordsForPersonsAsync([studentId.Value], date, ct), caller.InstitutionId, ct);
    }

    public async Task<IReadOnlyList<AttendanceView>> ForLinkedStudentsAsync(DateOnly? date, AttendanceCaller caller, CancellationToken ct)
    {
        // BR-066 pattern (API-019): a Parent sees only linked children.
        var studentIds = await repository.FindLinkedStudentIdsAsync(caller.UserId, ct);
        if (studentIds.Count == 0) return [];
        return await ToViewsAsync(await repository.FindRecordsForPersonsAsync(studentIds, date, ct), caller.InstitutionId, ct);
    }

    public async Task<int> RunLockSweepAsync(CancellationToken ct)
    {
        var utcNow = clock.GetUtcNow();
        var nowUtc = utcNow.UtcDateTime;
        var swept = 0;

        foreach (var (request, record, zoneId) in await repository.ListPendingCorrectionsAcrossInstitutionsAsync(ct))
        {
            var today = AttendanceCalendar.LocalToday(utcNow, AttendanceCalendar.ResolveTimeZone(zoneId, out _));
            if (!AttendanceCalendar.IsLocked(AttendanceCalendar.DaysSince(record.AttendanceDate, today))) continue;

            AutoReject(request, record, nowUtc);
            swept++;
        }

        if (swept > 0) await repository.SaveChangesAsync(ct);
        return swept;
    }
}
