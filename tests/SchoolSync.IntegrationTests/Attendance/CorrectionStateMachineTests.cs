using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Attendance;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Attendance;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.Attendance;

// CT-202 — the correction/lock state machine, 07-business-logic/attendance.md. The Unit-layer rows
// of 14-test-spec/attendance.md: TC-405, 406, 408, 409, 410, 411. Service over its real repository
// and an EF InMemory context, with a settable clock, so "N days ago" is a real elapsed calendar
// distance and never a back-dated row pretending to be one.
public class CorrectionStateMachineTests : IDisposable
{
    private static readonly Guid InstitutionId = Guid.NewGuid();
    private static readonly Guid ClassId = Guid.NewGuid();
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid TeacherUserId = Guid.NewGuid();
    private static readonly Guid PrincipalUserId = Guid.NewGuid();

    // 06:00 UTC = 11:30 Asia/Kolkata (Q-215 fallback: no configuration row is seeded).
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 9, 14, 6, 0, 0, TimeSpan.Zero));
    private static readonly DateOnly Today = new(2026, 9, 14);

    private readonly SchoolSyncDbContext _db;
    private readonly AttendanceService _service;
    private readonly AttendanceCaller _teacher = new(TeacherUserId, UserRole.Teacher, InstitutionId);
    private readonly AttendanceCaller _principal = new(PrincipalUserId, UserRole.Principal, InstitutionId);

    public CorrectionStateMachineTests()
    {
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new SchoolSyncDbContext(options, new FakeCurrentTenant { InstitutionId = InstitutionId });
        _db.Teachers.Add(new Teacher
        {
            Id = TeacherId,
            InstitutionId = InstitutionId,
            Name = "Meera Rao",
            ContactEmail = "meera@example.edu",
            Telephone = "+919000000002",
            Gender = "Female",
            Address = "12 Staff Lane",
            JoiningDate = new DateOnly(2024, 6, 1)
        });
        _db.UserAccounts.Add(new UserAccount
        {
            Id = TeacherUserId,
            InstitutionId = InstitutionId,
            Role = UserRole.Teacher,
            Username = "meera",
            Email = "meera.login@example.edu",
            PasswordHash = "x",
            TeacherId = TeacherId
        });
        _db.Classes.Add(new Class { Id = ClassId, InstitutionId = InstitutionId, Name = "Grade 5", Section = "A", ClassTeacherId = TeacherId });
        _db.SaveChanges();
        _service = new AttendanceService(new AttendanceRepository(_db), _clock);
    }

    public void Dispose() => _db.Dispose();

    private AttendanceRecord SeedRecord(int daysAgo, Guid? recordedBy = null, AttendanceOutcome outcome = AttendanceOutcome.Absent)
    {
        var studentId = Guid.NewGuid();
        _db.Students.Add(new Student
        {
            Id = studentId,
            InstitutionId = InstitutionId,
            ClassId = ClassId,
            SrNumber = $"SR-{studentId:N}",
            RollNumber = $"R-{studentId:N}",
            Name = "Asha Menon",
            DateOfBirth = new DateOnly(2016, 4, 2),
            Gender = "Female",
            Email = $"{studentId:N}@example.edu"
        });
        var record = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            InstitutionId = InstitutionId,
            PersonId = studentId,
            PersonType = PersonType.Student,
            AttendanceDate = Today.AddDays(-daysAgo),
            Outcome = outcome,
            RecordedByUserId = recordedBy ?? TeacherUserId,
            RecordedAtUtc = _clock.GetUtcNow().UtcDateTime,
            CorrectionStatus = CorrectionStatus.None
        };
        _db.AttendanceRecords.Add(record);
        _db.SaveChanges();
        return record;
    }

    private Task<AttendanceResult<CorrectionResult>> RequestAsync(AttendanceRecord record, AttendanceCaller caller, string outcome = "Present") =>
        _service.RequestCorrectionAsync(record.Id, outcome, "Marked wrongly", caller, CancellationToken.None);

    private async Task<AttendanceRecord> ReloadAsync(Guid recordId)
    {
        _db.ChangeTracker.Clear();
        return await _db.AttendanceRecords.SingleAsync(r => r.Id == recordId);
    }

    [Fact] // TC-405 — day 0, the original recorder self-corrects: direct update, no CorrectionPending
    public async Task A_same_day_self_correction_applies_directly_with_no_pending_request()
    {
        var record = SeedRecord(daysAgo: 0);

        var viaBulk = await _service.MarkAsync(
            new MarkRequest(ClassId, Today, [new MarkEntry(record.PersonId, "Student", "Late")]), _teacher, CancellationToken.None);
        var viaRequest = await RequestAsync(record, _teacher, "Present");

        Assert.True(viaBulk.Succeeded);
        Assert.Equal(CorrectionRequestStatus.AppliedDirectly, viaRequest.Value!.Status);
        var stored = await ReloadAsync(record.Id);
        Assert.Equal(AttendanceOutcome.Present, stored.Outcome);
        Assert.Equal(CorrectionStatus.None, stored.CorrectionStatus);
        Assert.DoesNotContain(await _db.CorrectionRequests.ToListAsync(), r => r.Status == CorrectionRequestStatus.Pending);
    }

    [Fact] // SPEC-FR-033 negative: day 0 direct correction is the original recorder's, not any teacher's
    public async Task A_teacher_cannot_directly_correct_a_same_day_record_someone_else_recorded()
    {
        var record = SeedRecord(daysAgo: 0, recordedBy: PrincipalUserId);

        var result = await RequestAsync(record, _teacher);

        Assert.Equal(AttendanceFailureReason.Forbidden, result.Failure);
        Assert.Empty(await _db.CorrectionRequests.ToListAsync());
    }

    [Fact] // TC-406 — adversarial: fails against an implementation that applies the Teacher's day-5 edit directly
    public async Task A_teacher_day_5_correction_creates_a_pending_request_and_leaves_the_record_unchanged()
    {
        var record = SeedRecord(daysAgo: 5, outcome: AttendanceOutcome.Absent);

        var result = await RequestAsync(record, _teacher, "Present");

        Assert.True(result.Succeeded);
        Assert.Equal(CorrectionRequestStatus.Pending, result.Value!.Status);
        var stored = await ReloadAsync(record.Id);
        Assert.Equal(AttendanceOutcome.Absent, stored.Outcome); // NOT applied
        Assert.Equal(CorrectionStatus.CorrectionPending, stored.CorrectionStatus);
    }

    [Theory] // TC-408 — adversarial: fails against >=30, or against creating a pending request anyway
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Principal)]
    public async Task A_record_31_days_old_is_locked_for_every_role_and_no_request_is_created(UserRole role)
    {
        var record = SeedRecord(daysAgo: 31);
        var caller = role == UserRole.Teacher ? _teacher : _principal;

        var result = await RequestAsync(record, caller);

        Assert.Equal(AttendanceFailureReason.Locked, result.Failure);
        Assert.Empty(await _db.CorrectionRequests.ToListAsync());
        Assert.Equal(AttendanceOutcome.Absent, (await ReloadAsync(record.Id)).Outcome);
    }

    [Theory] // TC-409 — boundary: day 29 still within the window; Q-214: so is day 30
    [InlineData(1)]
    [InlineData(29)]
    [InlineData(30)]
    public async Task A_teacher_request_inside_the_window_is_pending(int daysAgo)
    {
        var record = SeedRecord(daysAgo);

        var result = await RequestAsync(record, _teacher);

        Assert.Equal(CorrectionRequestStatus.Pending, result.Value!.Status);
    }

    [Fact] // TC-410 — adversarial: fails against an implementation with no sweep
    public async Task The_sweep_auto_rejects_a_request_once_its_record_locks_and_not_a_day_before()
    {
        var record = SeedRecord(daysAgo: 29);
        var request = (await RequestAsync(record, _teacher)).Value!;

        _clock.Advance(TimeSpan.FromDays(1)); // the record is now 30 days old — still correctable (Q-214)
        Assert.Equal(0, await _service.RunLockSweepAsync(CancellationToken.None));
        Assert.Equal(CorrectionRequestStatus.Pending, (await _db.CorrectionRequests.SingleAsync(r => r.Id == request.Id)).Status);

        _clock.Advance(TimeSpan.FromDays(1)); // 31 days: locked
        Assert.Equal(1, await _service.RunLockSweepAsync(CancellationToken.None));

        _db.ChangeTracker.Clear();
        var swept = await _db.CorrectionRequests.SingleAsync(r => r.Id == request.Id);
        Assert.Equal(CorrectionRequestStatus.AutoRejected, swept.Status); // rejected, never silently approved
        Assert.Null(swept.ResolvedByUserId);
        var stored = await _db.AttendanceRecords.SingleAsync(r => r.Id == record.Id);
        Assert.Equal(AttendanceOutcome.Absent, stored.Outcome);
        Assert.Equal(CorrectionStatus.None, stored.CorrectionStatus);
    }

    [Fact] // TC-410 companion: the sweep works across institutions (ADR-0005) on each one's own calendar
    public async Task The_sweep_reaches_other_institutions_pending_requests()
    {
        var otherInstitution = Guid.NewGuid();
        var foreignRecord = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            InstitutionId = otherInstitution,
            PersonId = Guid.NewGuid(),
            PersonType = PersonType.Student,
            AttendanceDate = Today.AddDays(-31),
            Outcome = AttendanceOutcome.Absent,
            RecordedByUserId = Guid.NewGuid(),
            RecordedAtUtc = DateTime.UtcNow,
            CorrectionStatus = CorrectionStatus.CorrectionPending
        };
        _db.AttendanceRecords.Add(foreignRecord);
        _db.CorrectionRequests.Add(new CorrectionRequest
        {
            Id = Guid.NewGuid(),
            InstitutionId = otherInstitution,
            AttendanceRecordId = foreignRecord.Id,
            RequestedOutcome = AttendanceOutcome.Present,
            Reason = "x",
            RequestedByUserId = Guid.NewGuid(),
            RequestedAtUtc = DateTime.UtcNow,
            Status = CorrectionRequestStatus.Pending
        });
        await _db.SaveChangesAsync();

        Assert.Equal(1, await _service.RunLockSweepAsync(CancellationToken.None));
    }

    [Fact] // TC-411 — adversarial: only one CorrectionPending per record
    public async Task A_second_request_while_the_first_is_pending_is_rejected()
    {
        var record = SeedRecord(daysAgo: 5);
        Assert.True((await RequestAsync(record, _teacher, "Present")).Succeeded);

        var second = await RequestAsync(record, _teacher, "Late");

        Assert.Equal(AttendanceFailureReason.ValidationFailed, second.Failure);
        Assert.Single(await _db.CorrectionRequests.ToListAsync());
    }

    [Fact] // Q-212 "both": approving after the boundary re-checks the lock regardless of the sweep
    public async Task Approving_a_request_whose_record_locked_before_the_sweep_ran_is_refused_and_auto_rejects_it()
    {
        var record = SeedRecord(daysAgo: 30);
        var request = (await RequestAsync(record, _teacher, "Present")).Value!;
        _clock.Advance(TimeSpan.FromDays(1)); // locked; no sweep has run

        var approve = await _service.ApproveCorrectionAsync(request.Id, _principal, CancellationToken.None);

        Assert.Equal(AttendanceFailureReason.Locked, approve.Failure);
        _db.ChangeTracker.Clear();
        Assert.Equal(CorrectionRequestStatus.AutoRejected, (await _db.CorrectionRequests.SingleAsync()).Status);
        Assert.Equal(AttendanceOutcome.Absent, (await _db.AttendanceRecords.SingleAsync(r => r.Id == record.Id)).Outcome);
    }

    [Fact] // PF-007 step 4: reject leaves the record unchanged and records the reason
    public async Task Rejecting_leaves_the_record_unchanged_and_requires_a_reason()
    {
        var record = SeedRecord(daysAgo: 5);
        var request = (await RequestAsync(record, _teacher, "Present")).Value!;

        Assert.Equal(AttendanceFailureReason.ValidationFailed,
            (await _service.RejectCorrectionAsync(request.Id, "  ", _principal, CancellationToken.None)).Failure);

        var rejected = await _service.RejectCorrectionAsync(request.Id, "Register shows absent", _principal, CancellationToken.None);

        Assert.Equal(CorrectionRequestStatus.Rejected, rejected.Value!.Status);
        Assert.Equal("Register shows absent", rejected.Value.ResolutionNote);
        var stored = await ReloadAsync(record.Id);
        Assert.Equal(AttendanceOutcome.Absent, stored.Outcome);
        Assert.Equal(CorrectionStatus.None, stored.CorrectionStatus);
    }

    [Fact] // A resolved request cannot be resolved again (approve after reject)
    public async Task A_resolved_request_cannot_be_approved_afterwards()
    {
        var record = SeedRecord(daysAgo: 5);
        var request = (await RequestAsync(record, _teacher, "Present")).Value!;
        await _service.RejectCorrectionAsync(request.Id, "No", _principal, CancellationToken.None);

        var approve = await _service.ApproveCorrectionAsync(request.Id, _principal, CancellationToken.None);

        Assert.Equal(AttendanceFailureReason.ValidationFailed, approve.Failure);
        Assert.Equal(AttendanceOutcome.Absent, (await ReloadAsync(record.Id)).Outcome);
    }

    [Fact] // Q-205 + supersede: a leadership direct write closes the stale pending request
    public async Task A_leadership_bulk_write_supersedes_a_pending_teacher_request()
    {
        var record = SeedRecord(daysAgo: 5, outcome: AttendanceOutcome.Absent);
        var request = (await RequestAsync(record, _teacher, "Present")).Value!;

        var write = await _service.MarkAsync(
            new MarkRequest(ClassId, record.AttendanceDate, [new MarkEntry(record.PersonId, "Student", "Late")]), _principal, CancellationToken.None);
        Assert.True(write.Succeeded);

        // Approving the stale request afterwards must not overwrite leadership's Late with Present.
        var approve = await _service.ApproveCorrectionAsync(request.Id, _principal, CancellationToken.None);
        Assert.Equal(AttendanceFailureReason.ValidationFailed, approve.Failure);

        _db.ChangeTracker.Clear();
        Assert.Equal(CorrectionRequestStatus.Superseded, (await _db.CorrectionRequests.SingleAsync()).Status);
        Assert.Equal(AttendanceOutcome.Late, (await _db.AttendanceRecords.SingleAsync(r => r.Id == record.Id)).Outcome);
    }

    [Fact] // BRL-033: a teacher cannot correct staff attendance, and only leadership resolves (Q-204)
    public async Task Teachers_cannot_correct_staff_records_or_resolve_requests()
    {
        var staffRecord = new AttendanceRecord
        {
            Id = Guid.NewGuid(),
            InstitutionId = InstitutionId,
            PersonId = TeacherId,
            PersonType = PersonType.Teacher,
            AttendanceDate = Today.AddDays(-2),
            Outcome = AttendanceOutcome.Absent,
            RecordedByUserId = PrincipalUserId,
            RecordedAtUtc = DateTime.UtcNow,
            CorrectionStatus = CorrectionStatus.None
        };
        _db.AttendanceRecords.Add(staffRecord);
        await _db.SaveChangesAsync();
        Assert.Equal(AttendanceFailureReason.Forbidden, (await RequestAsync(staffRecord, _teacher)).Failure);

        var studentRecord = SeedRecord(daysAgo: 5);
        var request = (await RequestAsync(studentRecord, _teacher)).Value!;
        Assert.Equal(AttendanceFailureReason.Forbidden,
            (await _service.ApproveCorrectionAsync(request.Id, _teacher, CancellationToken.None)).Failure);
    }
}
