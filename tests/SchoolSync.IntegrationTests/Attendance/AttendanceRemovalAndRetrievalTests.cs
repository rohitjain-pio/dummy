using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SchoolSync.Domain.Entities;
using Xunit;

namespace SchoolSync.IntegrationTests.Attendance;

// CT-203 — API-035 (removal) and API-036 (retrieval), contract tests per 15-task-breakdown.md.
// DoD: "Leadership-only removal enforced".
public class AttendanceRemovalAndRetrievalTests : IAsyncLifetime
{
    private AttendanceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId;
    private Guid _classId;
    private Guid _otherClassId;
    private Guid _teacherUserId;
    private Guid _studentA; // in _classId
    private Guid _studentB; // in _otherClassId
    private static readonly DateOnly Today = new(2026, 9, 14);

    public async Task InitializeAsync()
    {
        _factory = new AttendanceApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        _classId = await _factory.SeedClassAsync(_institutionId, "Grade 5", "A");
        _otherClassId = await _factory.SeedClassAsync(_institutionId, "Grade 6", "B");
        var (teacherId, teacherUserId) = await _factory.SeedTeacherWithAccountAsync(_institutionId);
        _teacherUserId = teacherUserId;
        await _factory.MakeClassTeacherAsync(_classId, teacherId);
        _studentA = await _factory.SeedStudentAsync(_institutionId, _classId, "SR-A", "R-A");
        _studentB = await _factory.SeedStudentAsync(_institutionId, _otherClassId, "SR-B", "R-B");
        await _factory.SeedRecordAsync(_institutionId, _studentA, Today, AttendanceOutcome.Present, teacherUserId);
        await _factory.SeedRecordAsync(_institutionId, _studentB, Today, AttendanceOutcome.Absent, Guid.NewGuid());
        await _factory.SeedRecordAsync(_institutionId, teacherId, Today, AttendanceOutcome.Present, Guid.NewGuid(), PersonType.Teacher);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role, Guid? institutionId = null) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, institutionId ?? _institutionId));

    private record ViewRow(Guid Id, Guid PersonId, string PersonType, DateOnly AttendanceDate, string Outcome, string CorrectionStatus, bool Locked);
    private record ErrorBody(string Code, string Message);

    private Task<Guid> RecordIdForAsync(Guid personId) =>
        _factory.QueryAsync(db => db.AttendanceRecords.IgnoreQueryFilters().Where(r => r.PersonId == personId).Select(r => r.Id).SingleAsync());

    [Theory] // API-035 — adversarial: DoD "leadership-only removal enforced"
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Parent)]
    public async Task Non_leadership_roles_cannot_remove_an_attendance_entry(UserRole role)
    {
        var recordId = await RecordIdForAsync(_studentA);
        AuthenticateAs(role == UserRole.Teacher ? _teacherUserId : Guid.NewGuid(), role); // even the recording teacher

        var response = await _client.DeleteAsync($"/api/v1/attendance/{recordId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, await _factory.QueryAsync(db => db.AttendanceRecords.IgnoreQueryFilters().CountAsync(r => r.Id == recordId)));
    }

    [Fact] // API-035 positive: 204, row gone, correction requests cascade, Delete audited with a before-image
    public async Task Leadership_removes_an_entry_with_its_correction_requests_and_the_removal_is_audited()
    {
        var recordId = await RecordIdForAsync(_studentB);
        await _factory.QueryAsync(async db =>
        {
            db.CorrectionRequests.Add(new CorrectionRequest
            {
                Id = Guid.NewGuid(),
                InstitutionId = _institutionId,
                AttendanceRecordId = recordId,
                RequestedOutcome = AttendanceOutcome.Present,
                Reason = "x",
                RequestedByUserId = Guid.NewGuid(),
                RequestedAtUtc = DateTime.UtcNow,
                Status = CorrectionRequestStatus.Rejected
            });
            return await db.SaveChangesAsync();
        });
        var principalUserId = Guid.NewGuid();
        AuthenticateAs(principalUserId, UserRole.Principal);

        var response = await _client.DeleteAsync($"/api/v1/attendance/{recordId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await _factory.QueryAsync(db => db.AttendanceRecords.IgnoreQueryFilters().CountAsync(r => r.Id == recordId)));
        Assert.Equal(0, await _factory.QueryAsync(db => db.CorrectionRequests.IgnoreQueryFilters().CountAsync(r => r.AttendanceRecordId == recordId)));
        var audit = await _factory.QueryAsync(db => db.AuditLogs.SingleAsync(a => a.EntityId == recordId.ToString() && a.Action == "Delete"));
        Assert.Equal(principalUserId, audit.ActorUserId);
        // The before-image identifies the removed row. (It carries enums as ordinals — CT-003's
        // serializer, recorded as a finding for review rather than changed inside CT-203.)
        Assert.Contains(_studentB.ToString(), audit.BeforeSnapshot);
        // BRL-035: the request history's removal is audited too, not deleted silently by a DB cascade.
        Assert.Equal(1, await _factory.QueryAsync(db => db.AuditLogs.CountAsync(a => a.EntityType == nameof(CorrectionRequest) && a.Action == "Delete")));
    }

    [Fact] // Locked is terminal for every role — removal included
    public async Task A_locked_entry_cannot_be_removed_even_by_leadership()
    {
        var student = await _factory.SeedStudentAsync(_institutionId, _classId, "SR-OLD", "R-OLD");
        await _factory.SeedRecordAsync(_institutionId, student, Today.AddDays(-31), AttendanceOutcome.Absent, Guid.NewGuid());
        var recordId = await RecordIdForAsync(student);
        AuthenticateAs(Guid.NewGuid(), UserRole.Administrator);

        var response = await _client.DeleteAsync($"/api/v1/attendance/{recordId}");

        Assert.Equal((HttpStatusCode)423, response.StatusCode);
        Assert.Equal("ERR-401", (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Code);
    }

    [Fact] // API-035 on an unknown or foreign id: 404
    public async Task Removing_an_unknown_entry_returns_404()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/v1/attendance/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact] // API-036 — leadership sees the institutional list, staff records included, with derived `locked`
    public async Task Leadership_retrieves_the_institutional_list_and_can_filter()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);

        var all = (await _client.GetFromJsonAsync<List<ViewRow>>("/api/v1/attendance"))!;
        Assert.Equal(3, all.Count);
        Assert.Contains(all, r => r.PersonType == "Teacher");
        Assert.All(all, r => Assert.False(r.Locked));

        var byPerson = (await _client.GetFromJsonAsync<List<ViewRow>>($"/api/v1/attendance?personId={_studentB}"))!;
        Assert.Equal(_studentB, Assert.Single(byPerson).PersonId);

        var byClass = (await _client.GetFromJsonAsync<List<ViewRow>>($"/api/v1/attendance?classId={_classId}&date={Today:yyyy-MM-dd}"))!;
        Assert.Equal(_studentA, Assert.Single(byClass).PersonId);

        var otherDay = (await _client.GetFromJsonAsync<List<ViewRow>>($"/api/v1/attendance?date={Today.AddDays(-1):yyyy-MM-dd}"))!;
        Assert.Empty(otherDay);
    }

    [Fact] // API-036 — adversarial: a Teacher's own-class scope can't be widened by omitting or swapping classId
    public async Task A_teacher_sees_only_their_own_class_and_must_name_it()
    {
        AuthenticateAs(_teacherUserId, UserRole.Teacher);

        var own = (await _client.GetFromJsonAsync<List<ViewRow>>($"/api/v1/attendance?classId={_classId}"))!;
        Assert.Equal(_studentA, Assert.Single(own).PersonId); // no staff record, no other class

        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/attendance")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/v1/attendance?classId={_otherClassId}")).StatusCode);

        // personId of a student in another class, scoped by the teacher's own class: nothing comes back.
        var sneaky = (await _client.GetFromJsonAsync<List<ViewRow>>($"/api/v1/attendance?classId={_classId}&personId={_studentB}"))!;
        Assert.Empty(sneaky);
    }

    [Fact] // API-036 /me — a Student sees only their own records
    public async Task A_student_sees_only_their_own_attendance()
    {
        var studentUserId = await _factory.SeedAccountAsync(_institutionId, UserRole.Student, _studentA);
        AuthenticateAs(studentUserId, UserRole.Student);

        var rows = (await _client.GetFromJsonAsync<List<ViewRow>>("/api/v1/attendance/me"))!;

        Assert.Equal(_studentA, Assert.Single(rows).PersonId);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/attendance")).StatusCode);
    }

    [Fact] // API-036 /linked — a Parent sees linked children only; an unlinked parent sees nothing
    public async Task A_parent_sees_only_linked_childrens_attendance()
    {
        await _factory.SetPlanAsync(_institutionId, SchoolSync.Infrastructure.Persistence.SchoolSyncDbContext.BespokePlanId); // CT-503: ParentAccess tier
        var parentUserId = await _factory.SeedAccountAsync(_institutionId, UserRole.Parent);
        await _factory.LinkParentAsync(parentUserId, _studentB);
        AuthenticateAs(parentUserId, UserRole.Parent);

        var rows = (await _client.GetFromJsonAsync<List<ViewRow>>("/api/v1/attendance/linked"))!;
        Assert.Equal(_studentB, Assert.Single(rows).PersonId);

        var unlinkedParent = await _factory.SeedAccountAsync(_institutionId, UserRole.Parent);
        AuthenticateAs(unlinkedParent, UserRole.Parent);
        Assert.Empty((await _client.GetFromJsonAsync<List<ViewRow>>("/api/v1/attendance/linked"))!);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/attendance/me")).StatusCode);
    }

    [Fact] // BRL-032: another institution's leadership sees none of these records
    public async Task Another_institutions_leadership_sees_nothing()
    {
        var otherInstitution = await _factory.SeedInstitutionAsync("Other School");
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal, otherInstitution);

        Assert.Empty((await _client.GetFromJsonAsync<List<ViewRow>>("/api/v1/attendance"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/v1/attendance/{await RecordIdForAsync(_studentA)}")).StatusCode);
    }
}
