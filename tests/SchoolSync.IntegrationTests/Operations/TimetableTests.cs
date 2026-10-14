using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Operations;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Operations;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Attendance;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.Operations;

// CT-403 Unit-layer rows: TC-605, 606, 607, 608. The service over its real repository and an EF
// InMemory context — which does NOT enforce the unique indexes, so every rejection here is the
// application-level decision table, not the database backstop.
public class TimetableServiceTests : IDisposable
{
    private static readonly Guid InstitutionId = Guid.NewGuid();
    private static readonly Guid ClassA = Guid.NewGuid(), ClassB = Guid.NewGuid(), Maths = Guid.NewGuid();
    private static readonly Guid Meera = Guid.NewGuid(), Arjun = Guid.NewGuid();

    private readonly SchoolSyncDbContext _db;
    private readonly TimetableService _service;
    private readonly OperationsCaller _principal = new(Guid.NewGuid(), UserRole.Principal, InstitutionId);

    public TimetableServiceTests()
    {
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new SchoolSyncDbContext(options, new FakeCurrentTenant { InstitutionId = InstitutionId });
        _db.Classes.AddRange(new Class { Id = ClassA, InstitutionId = InstitutionId, Name = "Grade 5", Section = "A" },
                             new Class { Id = ClassB, InstitutionId = InstitutionId, Name = "Grade 6", Section = "B" });
        _db.Subjects.Add(new Subject { Id = Maths, InstitutionId = InstitutionId, Name = "Mathematics" });
        foreach (var (id, name) in new[] { (Meera, "Meera"), (Arjun, "Arjun") })
            _db.Teachers.Add(new Teacher { Id = id, InstitutionId = InstitutionId, Name = name, ContactEmail = $"{name}@x.edu", Telephone = "+919000000000", Gender = "F", Address = "a", JoiningDate = new DateOnly(2024, 1, 1) });
        _db.SaveChanges();
        _service = new TimetableService(new TimetableRepository(_db), new TestClock());
    }

    public void Dispose() => _db.Dispose();

    private static PeriodFields Period(Guid classId, Guid teacherId, string room, string day = "Monday", string slot = "09:00") =>
        new(classId, Maths, teacherId, room, day, slot);

    private async Task<PeriodView> SeedAsync(PeriodFields fields)
    {
        var result = await _service.CreateAsync(fields, _principal, CancellationToken.None);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    [Fact] // TC-605 — teacher already scheduled at (Mon, 09:00) elsewhere -> ERR-701
    public async Task A_teacher_double_booked_in_the_same_slot_is_rejected()
    {
        await SeedAsync(Period(ClassA, Meera, "R1"));
        var result = await _service.CreateAsync(Period(ClassB, Meera, "R2"), _principal, CancellationToken.None);
        Assert.Equal([ScheduleConflict.Teacher], result.Conflicts);
    }

    [Fact] // TC-606 — class already scheduled at (Mon, 09:00) -> ERR-702
    public async Task A_class_double_booked_in_the_same_slot_is_rejected()
    {
        await SeedAsync(Period(ClassA, Meera, "R1"));
        var result = await _service.CreateAsync(Period(ClassA, Arjun, "R2"), _principal, CancellationToken.None);
        Assert.Equal([ScheduleConflict.Class], result.Conflicts);
    }

    [Fact] // TC-607 — room already booked at (Mon, 09:00) -> ERR-703, matched case- and space-insensitively
    public async Task A_room_double_booked_in_the_same_slot_is_rejected()
    {
        await SeedAsync(Period(ClassA, Meera, "Lab 1"));
        var result = await _service.CreateAsync(Period(ClassB, Arjun, "  lab 1 "), _principal, CancellationToken.None);
        Assert.Equal([ScheduleConflict.Room], result.Conflicts);
    }

    [Fact] // TC-608 — adversarial: teacher AND room conflict -> both reported in one response
    public async Task A_submission_conflicting_on_teacher_and_room_reports_both_at_once()
    {
        await SeedAsync(Period(ClassA, Meera, "Lab 1"));

        var result = await _service.CreateAsync(Period(ClassB, Meera, "Lab 1"), _principal, CancellationToken.None);

        Assert.Equal(TimetableFailureReason.Conflict, result.Failure);
        Assert.Equal([ScheduleConflict.Teacher, ScheduleConflict.Room], result.Conflicts); // not just the first
        Assert.Single(await _db.TimetablePeriods.ToListAsync()); // nothing saved
    }

    [Fact] // Decision table row 1 + slot key: a different day or time is no conflict at all
    public async Task Same_teacher_class_and_room_in_a_different_slot_saves()
    {
        await SeedAsync(Period(ClassA, Meera, "Lab 1"));
        Assert.True((await _service.CreateAsync(Period(ClassA, Meera, "Lab 1", slot: "10:00"), _principal, CancellationToken.None)).Succeeded);
        Assert.True((await _service.CreateAsync(Period(ClassA, Meera, "Lab 1", day: "Tuesday"), _principal, CancellationToken.None)).Succeeded);
    }

    [Fact] // An edit never conflicts with the period's own current slot
    public async Task Editing_a_period_in_place_does_not_conflict_with_itself()
    {
        var period = await SeedAsync(Period(ClassA, Meera, "Lab 1"));
        var result = await _service.UpdateAsync(period.Id, Period(ClassA, Meera, "Lab 2"), _principal, CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal("Lab 2", result.Value!.Room);
    }

    [Theory] // VAL-023 + Q-225: HH:mm only, so "9am" and "9:00" can never sit beside "09:00"
    [InlineData("9am")]
    [InlineData("9:00")]
    [InlineData("24:00")]
    [InlineData("")]
    public async Task Time_slots_must_be_24h_HH_mm(string slot) =>
        Assert.Equal(TimetableFailureReason.ValidationFailed, (await _service.CreateAsync(Period(ClassA, Meera, "R1", slot: slot), _principal, CancellationToken.None)).Failure);

    [Fact] // PF-011 pre-condition: the class, subject and teacher must exist in this institution
    public async Task Unknown_references_are_rejected()
    {
        Assert.Equal(TimetableFailureReason.ValidationFailed, (await _service.CreateAsync(Period(Guid.NewGuid(), Meera, "R1"), _principal, CancellationToken.None)).Failure);
        Assert.Equal(TimetableFailureReason.ValidationFailed, (await _service.CreateAsync(Period(ClassA, Guid.NewGuid(), "R1"), _principal, CancellationToken.None)).Failure);
    }
}

// CT-403 through HTTP: the 409 wire shape, BRL-056's role split, and Q-225's publish visibility.
public class TimetableEndpointsTests : IAsyncLifetime
{
    private AttendanceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId, _classA, _classB, _subject, _teacherId, _teacherUserId, _otherTeacherId;

    public async Task InitializeAsync()
    {
        _factory = new AttendanceApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        _classA = await _factory.SeedClassAsync(_institutionId, "Grade 5", "A");
        _classB = await _factory.SeedClassAsync(_institutionId, "Grade 6", "B");
        _subject = await _factory.SeedSubjectAsync(_institutionId);
        (_teacherId, _teacherUserId) = await _factory.SeedTeacherWithAccountAsync(_institutionId);
        (_otherTeacherId, _) = await _factory.SeedTeacherWithAccountAsync(_institutionId, "Arjun");
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, _institutionId));

    private object Body(Guid classId, Guid teacherId, string room, string slot = "09:00") =>
        new { classId, subjectId = _subject, teacherId, room, dayOfWeek = "Monday", timeSlot = slot };

    [Fact] // TC-608 on the wire: one 409 carrying both catalogue bodies
    public async Task A_double_conflict_returns_one_409_with_both_ERR_701_and_ERR_703()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/v1/timetable", Body(_classA, _teacherId, "Lab 1"))).StatusCode);

        var response = await _client.PostAsJsonAsync("/api/v1/timetable", Body(_classB, _teacherId, "Lab 1"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var codes = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("code").GetString()).ToList();
        Assert.Equal(["ERR-701", "ERR-703"], codes);
    }

    [Theory] // BRL-056: Teacher and learners are read-only
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Parent)]
    public async Task Non_leadership_cannot_create_or_publish_periods(UserRole role)
    {
        AuthenticateAs(Guid.NewGuid(), role);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/v1/timetable", Body(_classA, _teacherId, "R1"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsync($"/api/v1/timetable/{Guid.NewGuid()}/publish", null)).StatusCode);
    }

    [Fact] // Q-225 / FR-061-062: drafts are leadership-only; publishing makes a period visible by class and by teacher
    public async Task Unpublished_periods_are_hidden_from_staff_and_learners_until_published()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        var created = await (await _client.PostAsJsonAsync("/api/v1/timetable", Body(_classA, _teacherId, "R1"))).Content.ReadFromJsonAsync<JsonElement>();
        var periodId = created.GetProperty("id").GetGuid();
        await _client.PostAsJsonAsync("/api/v1/timetable", Body(_classB, _otherTeacherId, "R2")); // another draft, never published
        Assert.Equal(2, (await _client.GetFromJsonAsync<List<JsonElement>>("/api/v1/timetable"))!.Count); // leadership sees drafts

        AuthenticateAs(_teacherUserId, UserRole.Teacher);
        Assert.Empty((await _client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/timetable?classId={_classA}"))!);

        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        var published = await _client.PostAsync($"/api/v1/timetable/{periodId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.True((await published.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isPublished").GetBoolean());

        AuthenticateAs(Guid.NewGuid(), UserRole.Student);
        var byClass = (await _client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/timetable?classId={_classA}"))!;
        var byTeacher = (await _client.GetFromJsonAsync<List<JsonElement>>($"/api/v1/timetable?teacherId={_teacherId}"))!;
        Assert.Equal(periodId, Assert.Single(byClass).GetProperty("id").GetGuid());
        Assert.Equal(periodId, Assert.Single(byTeacher).GetProperty("id").GetGuid());
        Assert.Single((await _client.GetFromJsonAsync<List<JsonElement>>("/api/v1/timetable"))!); // the other draft stays hidden
    }

    [Fact] // NFR-028: schedule changes are audited
    public async Task Creating_and_publishing_a_period_writes_audit_rows()
    {
        var principal = Guid.NewGuid();
        AuthenticateAs(principal, UserRole.Principal);
        var id = (await (await _client.PostAsJsonAsync("/api/v1/timetable", Body(_classA, _teacherId, "R1"))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await _client.PostAsync($"/api/v1/timetable/{id}/publish", null);

        var audits = await _factory.QueryAsync(db => db.AuditLogs.Where(a => a.EntityType == nameof(TimetablePeriod) && a.EntityId == id.ToString()).ToListAsync());
        Assert.Equal(["Create", "Update"], audits.OrderBy(a => a.TimestampUtc).Select(a => a.Action));
        Assert.All(audits, a => Assert.Equal(principal, a.ActorUserId));
    }
}
