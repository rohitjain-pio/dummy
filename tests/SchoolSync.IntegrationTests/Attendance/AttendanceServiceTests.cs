using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Attendance;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Attendance;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.Attendance;

// 14-test-spec/attendance.md rows whose Layer is Unit. The service is exercised directly (no HTTP
// host), over its real repository and an EF InMemory context, because the property under test —
// "no duplicate row" — is a statement about what gets stored, which a hand-written fake would
// only restate.
public class AttendanceServiceTests : IDisposable
{
    private static readonly Guid InstitutionId = Guid.NewGuid();
    private static readonly Guid ClassId = Guid.NewGuid();
    private static readonly Guid LeadershipUserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 9, 14);

    private readonly SchoolSyncDbContext _db;
    private readonly TestClock _clock = new(new DateTimeOffset(2026, 9, 14, 6, 0, 0, TimeSpan.Zero));
    private readonly AttendanceService _service;
    private readonly AttendanceCaller _principal = new(LeadershipUserId, UserRole.Principal, InstitutionId);

    public AttendanceServiceTests()
    {
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new SchoolSyncDbContext(options, new FakeCurrentTenant { InstitutionId = InstitutionId });
        _db.Classes.Add(new Class { Id = ClassId, InstitutionId = InstitutionId, Name = "Grade 5", Section = "A" });
        _db.SaveChanges();
        _service = new AttendanceService(new AttendanceRepository(_db), _clock);
    }

    public void Dispose() => _db.Dispose();

    private Guid AddStudent()
    {
        var id = Guid.NewGuid();
        _db.Students.Add(new Student
        {
            Id = id,
            InstitutionId = InstitutionId,
            ClassId = ClassId,
            SrNumber = $"SR-{id:N}",
            RollNumber = $"R-{id:N}",
            Name = "Asha Menon",
            DateOfBirth = new DateOnly(2016, 4, 2),
            Gender = "Female",
            Email = $"{id:N}@example.edu"
        });
        _db.SaveChanges();
        return id;
    }

    private Task<AttendanceResult<IReadOnlyList<MarkedEntry>>> MarkAsync(Guid studentId, string outcome) =>
        _service.MarkAsync(new MarkRequest(ClassId, Today, [new MarkEntry(studentId, "Student", outcome)]), _principal, CancellationToken.None);

    [Fact] // TC-403 — adversarial: fails against an implementation using plain INSERT
    public async Task Resubmitting_the_same_student_and_date_with_a_different_outcome_updates_in_place()
    {
        var studentId = AddStudent();

        var first = await MarkAsync(studentId, "Absent");
        var second = await MarkAsync(studentId, "Present");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(MarkEntryResult.Updated, second.Value!.Single().Result);
        Assert.Equal(first.Value!.Single().RecordId, second.Value!.Single().RecordId); // same row, not a new id

        _db.ChangeTracker.Clear(); // read what was stored, not what is tracked
        var stored = Assert.Single(await _db.AttendanceRecords.Where(r => r.PersonId == studentId).ToListAsync());
        Assert.Equal(AttendanceOutcome.Present, stored.Outcome);
    }

    [Fact] // TC-403 companion: an identical resubmission is reported Unchanged, not Updated
    public async Task Resubmitting_the_same_outcome_is_unchanged()
    {
        var studentId = AddStudent();
        await MarkAsync(studentId, "Late");

        var again = await MarkAsync(studentId, "Late");

        Assert.Equal(MarkEntryResult.Unchanged, again.Value!.Single().Result);
        Assert.Single(await _db.AttendanceRecords.ToListAsync());
    }

    [Theory] // Q-215 fallback and CFG-004 boundary arithmetic, independent of the HTTP host
    [InlineData("Asia/Kolkata", 2026, 9, 13, 19, 0, 14)] // 00:30 local on the 14th
    [InlineData("America/New_York", 2026, 9, 14, 2, 0, 13)] // 22:00 local on the 13th
    [InlineData(null, 2026, 9, 13, 19, 0, 14)] // no configuration row -> Asia/Kolkata fallback
    [InlineData("Not/AZone", 2026, 9, 13, 19, 0, 14)] // unresolvable id -> fallback
    public void Local_today_uses_the_institution_zone_or_the_platform_fallback(
        string? zoneId, int y, int m, int d, int h, int min, int expectedDay)
    {
        var zone = AttendanceCalendar.ResolveTimeZone(zoneId, out _);
        var today = AttendanceCalendar.LocalToday(new DateTimeOffset(y, m, d, h, min, 0, TimeSpan.Zero), zone);
        Assert.Equal(new DateOnly(2026, 9, expectedDay), today);
    }

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, false)] // Q-214: day 30 is still correctable
    [InlineData(31, true)] // TC-408's ">30 not >=30"
    public void Lock_applies_strictly_after_30_days(int daysSince, bool locked) =>
        Assert.Equal(locked, AttendanceCalendar.IsLocked(daysSince));
}
