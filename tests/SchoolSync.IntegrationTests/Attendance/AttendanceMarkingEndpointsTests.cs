using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolSync.Domain.Entities;
using Xunit;

namespace SchoolSync.IntegrationTests.Attendance;

// CT-201 — API-031/032. 14-test-spec/attendance.md TC-401, TC-402, TC-404 (Integration), plus the
// BRL-033 / own-class / SPEC-FR-033 / CFG-004 / time-zone rules the bulk path must honour.
// TC-403 (Unit) is in AttendanceServiceTests.
public class AttendanceMarkingEndpointsTests : IAsyncLifetime
{
    private AttendanceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId;
    private Guid _classId;
    private Guid _teacherId;
    private Guid _teacherUserId;

    // Factory clock: 2026-09-14 11:30 Asia/Kolkata.
    private static readonly DateOnly Today = new(2026, 9, 14);

    public async Task InitializeAsync()
    {
        _factory = new AttendanceApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        _classId = await _factory.SeedClassAsync(_institutionId, "Grade 5", "A");
        (_teacherId, _teacherUserId) = await _factory.SeedTeacherWithAccountAsync(_institutionId);
        await _factory.MakeClassTeacherAsync(_classId, _teacherId);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role, Guid? institutionId = null) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", _factory.IssueTokenFor(userId, role, institutionId ?? _institutionId));

    private void AsClassTeacher() => AuthenticateAs(_teacherUserId, UserRole.Teacher);
    private void AsPrincipal() => AuthenticateAs(Guid.NewGuid(), UserRole.Principal);

    private async Task<List<Guid>> SeedStudentsAsync(int count, Guid? classId = null)
    {
        var ids = new List<Guid>();
        for (var i = 0; i < count; i++)
            ids.Add(await _factory.SeedStudentAsync(_institutionId, classId ?? _classId, $"SR-{Guid.NewGuid():N}", $"R-{i:D3}-{Guid.NewGuid():N}"));
        return ids;
    }

    private Task<HttpResponseMessage> MarkAsync(DateOnly date, IEnumerable<object> entries, Guid? classId = null) =>
        _client.PutAsJsonAsync("/api/v1/attendance", new { classId = classId ?? _classId, date, entries });

    private static object Student(Guid id, string outcome) => new { personId = id, personType = "Student", outcome };

    private record RosterRow(Guid StudentId, string Name, string? CurrentOutcome);
    private record MarkedRow(Guid RecordId, Guid PersonId, string PersonType, string Outcome, string Result);
    private record ErrorBody(string Code, string Message);

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Code);
    }

    [Fact] // TC-401 — adversarial: fails against an implementation that defaults to "Absent"
    public async Task A_fresh_roster_of_30_opens_with_every_student_NotYetMarked()
    {
        var students = await SeedStudentsAsync(30);
        AsClassTeacher();

        var response = await _client.GetAsync($"/api/v1/attendance/roster?classId={_classId}&date={Today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Absent", raw); // not even as a string anywhere in the payload
        var rows = (await response.Content.ReadFromJsonAsync<List<RosterRow>>())!;
        Assert.Equal(30, rows.Count);
        Assert.Equal(students.ToHashSet(), rows.Select(r => r.StudentId).ToHashSet());
        Assert.All(rows, r => Assert.Null(r.CurrentOutcome));
    }

    [Fact] // TC-401 companion: a marked student shows its outcome, the rest stay null
    public async Task Roster_shows_recorded_outcomes_and_leaves_unmarked_students_null()
    {
        var students = await SeedStudentsAsync(3);
        await _factory.SeedRecordAsync(_institutionId, students[0], Today, AttendanceOutcome.Late, _teacherUserId);
        AsClassTeacher();

        var rows = (await _client.GetFromJsonAsync<List<RosterRow>>($"/api/v1/attendance/roster?classId={_classId}"))!;

        Assert.Equal("Late", rows.Single(r => r.StudentId == students[0]).CurrentOutcome);
        Assert.All(rows.Where(r => r.StudentId != students[0]), r => Assert.Null(r.CurrentOutcome));
    }

    [Fact] // TC-402
    public async Task A_teacher_marks_30_students_in_one_request_creating_30_rows_and_one_audit_row_each()
    {
        var students = await SeedStudentsAsync(30);
        string[] outcomes = ["Present", "Absent", "Late", "Leave"];
        AsClassTeacher();

        var response = await MarkAsync(Today, students.Select((id, i) => Student(id, outcomes[i % 4])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var marked = (await response.Content.ReadFromJsonAsync<List<MarkedRow>>())!;
        Assert.Equal(30, marked.Count);
        Assert.All(marked, m => Assert.Equal("Created", m.Result));

        var rows = await _factory.QueryAsync(db => db.AttendanceRecords.IgnoreQueryFilters()
            .Where(r => r.InstitutionId == _institutionId).ToListAsync());
        Assert.Equal(30, rows.Count);
        Assert.All(rows, r => Assert.Equal(_teacherUserId, r.RecordedByUserId));

        var recordIds = rows.Select(r => r.Id.ToString()).ToHashSet();
        var audits = await _factory.QueryAsync(db => db.AuditLogs
            .Where(a => a.EntityType == nameof(AttendanceRecord)).ToListAsync());
        Assert.Equal(30, audits.Count(a => recordIds.Contains(a.EntityId)));
        Assert.All(audits, a => Assert.Equal(_teacherUserId, a.ActorUserId));
    }

    [Fact] // TC-404
    public async Task Attendance_for_tomorrow_is_rejected_with_ERR_403()
    {
        var students = await SeedStudentsAsync(1);
        AsClassTeacher();

        await AssertErrorAsync(await MarkAsync(Today.AddDays(1), [Student(students[0], "Present")]),
            HttpStatusCode.BadRequest, "ERR-403");
    }

    [Fact] // Guard: "same day" is the institution's local day, not UTC (07-business-logic "Guards, precisely")
    public async Task Just_after_local_midnight_today_is_the_new_local_date_even_though_UTC_is_still_yesterday()
    {
        var students = await SeedStudentsAsync(1);
        // 2026-09-13 19:00 UTC = 2026-09-14 00:30 Asia/Kolkata. A UTC-based "today" would call the
        // 14th a future date (ERR-403); the institution's own calendar says it is today.
        _factory.Clock.UtcNow = new DateTimeOffset(2026, 9, 13, 19, 0, 0, TimeSpan.Zero);
        AsClassTeacher();

        var response = await MarkAsync(Today, [Student(students[0], "Present")]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact] // Decision table, day 1-30, Teacher: no direct past-day write
    public async Task A_teacher_writing_yesterdays_attendance_directly_gets_ERR_402()
    {
        var students = await SeedStudentsAsync(1);
        AsClassTeacher();

        await AssertErrorAsync(await MarkAsync(Today.AddDays(-1), [Student(students[0], "Present")]),
            HttpStatusCode.UnprocessableEntity, "ERR-402");
    }

    [Theory] // Decision table, day 1-30, leadership applies immediately (Q-205); Q-214: day 30 is not locked
    [InlineData(1)]
    [InlineData(30)]
    public async Task Leadership_may_write_past_day_attendance_up_to_and_including_day_30(int daysAgo)
    {
        var students = await SeedStudentsAsync(1);
        AsPrincipal();

        var response = await MarkAsync(Today.AddDays(-daysAgo), [Student(students[0], "Present")]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact] // CFG-004: > 30 days is locked for every role, leadership included
    public async Task Day_31_is_locked_even_for_leadership()
    {
        var students = await SeedStudentsAsync(1);
        AsPrincipal();

        await AssertErrorAsync(await MarkAsync(Today.AddDays(-31), [Student(students[0], "Present")]),
            (HttpStatusCode)423, "ERR-401");
    }

    [Fact] // BRL-033: staff attendance is leadership only
    public async Task A_teacher_cannot_record_staff_attendance()
    {
        var (peerTeacherId, _) = await _factory.SeedTeacherWithAccountAsync(_institutionId, "Peer Teacher");
        AsClassTeacher();

        var response = await MarkAsync(Today, [new { personId = peerTeacherId, personType = "Teacher", outcome = "Present" }]);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "ERR-102");
    }

    [Fact] // SPEC-FR-032 positive control for the rule above
    public async Task Leadership_records_staff_attendance_without_a_class()
    {
        AsPrincipal();

        var response = await _client.PutAsJsonAsync("/api/v1/attendance", new
        {
            date = Today,
            entries = new[] { new { personId = _teacherId, personType = "Teacher", outcome = "Leave" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact] // PF-006 pre-condition: the teacher must be assigned to the class
    public async Task A_teacher_not_assigned_to_the_class_can_neither_open_its_roster_nor_mark_it()
    {
        var students = await SeedStudentsAsync(1);
        var (_, outsiderUserId) = await _factory.SeedTeacherWithAccountAsync(_institutionId, "Unassigned Teacher");
        AuthenticateAs(outsiderUserId, UserRole.Teacher);

        await AssertErrorAsync(await _client.GetAsync($"/api/v1/attendance/roster?classId={_classId}"), HttpStatusCode.Forbidden, "ERR-102");
        await AssertErrorAsync(await MarkAsync(Today, [Student(students[0], "Present")]), HttpStatusCode.Forbidden, "ERR-102");
    }

    [Fact] // PF-006 pre-condition, subject-mapping half: a subject teacher of the class is assigned
    public async Task A_subject_teacher_of_the_class_may_mark_it()
    {
        var students = await SeedStudentsAsync(1);
        var (subjectTeacherId, subjectTeacherUserId) = await _factory.SeedTeacherWithAccountAsync(_institutionId, "Subject Teacher");
        var subjectId = await _factory.SeedSubjectAsync(_institutionId);
        await _factory.QueryAsync(async db =>
        {
            db.SubjectTeacherClassMappings.Add(new SubjectTeacherClassMapping { SubjectId = subjectId, TeacherId = subjectTeacherId, ClassId = _classId });
            return await db.SaveChangesAsync();
        });
        AuthenticateAs(subjectTeacherUserId, UserRole.Teacher);

        Assert.Equal(HttpStatusCode.OK, (await MarkAsync(Today, [Student(students[0], "Present")])).StatusCode);
    }

    [Fact] // SPEC-FR-033: same-day direct correction belongs to the original recorder
    public async Task A_teacher_cannot_overwrite_an_outcome_another_user_recorded_today_but_may_correct_their_own()
    {
        var students = await SeedStudentsAsync(2);
        await _factory.SeedRecordAsync(_institutionId, students[0], Today, AttendanceOutcome.Absent, recordedByUserId: Guid.NewGuid());
        await _factory.SeedRecordAsync(_institutionId, students[1], Today, AttendanceOutcome.Absent, recordedByUserId: _teacherUserId);
        AsClassTeacher();

        await AssertErrorAsync(await MarkAsync(Today, [Student(students[0], "Present")]), HttpStatusCode.Forbidden, "ERR-102");

        var own = await MarkAsync(Today, [Student(students[1], "Present")]);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal("Updated", (await own.Content.ReadFromJsonAsync<List<MarkedRow>>())!.Single().Result);
    }

    [Theory] // VAL-013 / VAL-015 / request shape -> ERR-901, and nothing is written
    [InlineData("bad-outcome")]
    [InlineData("other-class-student")]
    [InlineData("duplicate-person")]
    [InlineData("withdrawn-student")]
    public async Task Invalid_entries_are_rejected_with_ERR_901_and_no_row_is_written(string scenario)
    {
        var students = await SeedStudentsAsync(1);
        var otherClass = await _factory.SeedClassAsync(_institutionId, "Grade 6", "B");
        var otherClassStudent = (await SeedStudentsAsync(1, otherClass))[0];
        var withdrawn = (await SeedStudentsAsync(1))[0];
        await _factory.QueryAsync(async db =>
        {
            (await db.Students.IgnoreQueryFilters().SingleAsync(s => s.Id == withdrawn)).IsActive = false;
            return await db.SaveChangesAsync();
        });
        AsClassTeacher();

        object[] entries = scenario switch
        {
            "bad-outcome" => [Student(students[0], "Absentt")],
            "other-class-student" => [Student(students[0], "Present"), Student(otherClassStudent, "Present")],
            "duplicate-person" => [Student(students[0], "Present"), Student(students[0], "Absent")],
            _ => [Student(withdrawn, "Present")]
        };

        await AssertErrorAsync(await MarkAsync(Today, entries), HttpStatusCode.BadRequest, "ERR-901");
        Assert.Equal(0, await _factory.QueryAsync(db => db.AttendanceRecords.IgnoreQueryFilters().CountAsync()));
    }

    [Fact] // BRL-032: another institution's class is invisible, not merely forbidden
    public async Task Another_institutions_roster_is_not_found()
    {
        var otherInstitution = await _factory.SeedInstitutionAsync("Other School");
        var otherClass = await _factory.SeedClassAsync(otherInstitution, "Grade 5", "A");
        AsPrincipal();

        await AssertErrorAsync(await _client.GetAsync($"/api/v1/attendance/roster?classId={otherClass}"), HttpStatusCode.NotFound, "ERR-902");
    }

    [Theory] // API-031/032 role gate
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Parent)]
    public async Task Students_and_parents_cannot_open_a_roster_or_mark(UserRole role)
    {
        AuthenticateAs(Guid.NewGuid(), role);

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/v1/attendance/roster?classId={_classId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await MarkAsync(Today, [Student(Guid.NewGuid(), "Present")])).StatusCode);
    }
}
