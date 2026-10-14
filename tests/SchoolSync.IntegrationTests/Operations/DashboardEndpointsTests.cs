using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Operations;
using SchoolSync.Domain.Entities;
using SchoolSync.IntegrationTests.Attendance;
using Xunit;

namespace SchoolSync.IntegrationTests.Operations;

// CT-401 — API-048/049. Gate: "contract tests per role variant". DoD: Student/Parent variants never
// expose institution-wide totals (BR-048). Also TC-609's dashboard half: no Satisfaction/Completion.
public class DashboardEndpointsTests : IAsyncLifetime
{
    private AttendanceApiFactory _factory = null!; // settable clock: 2026-09-14 11:30 Asia/Kolkata
    private HttpClient _client = null!;
    private static readonly DateOnly Today = new(2026, 9, 14);
    private Guid _institutionId, _classA, _classB, _teacherId, _teacherUserId;
    private Guid _a1, _a2, _b1; // a1/a2 in class A (a2 male), b1 in class B

    public async Task InitializeAsync()
    {
        _factory = new AttendanceApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        _classA = await _factory.SeedClassAsync(_institutionId, "Grade 5", "A");
        _classB = await _factory.SeedClassAsync(_institutionId, "Grade 6", "B");
        (_teacherId, _teacherUserId) = await _factory.SeedTeacherWithAccountAsync(_institutionId);
        await _factory.MakeClassTeacherAsync(_classA, _teacherId); // the teacher teaches class A only
        _a1 = await _factory.SeedStudentAsync(_institutionId, _classA, "SR-A1", "R-A1");
        _a2 = await _factory.SeedStudentAsync(_institutionId, _classA, "SR-A2", "R-A2");
        _b1 = await _factory.SeedStudentAsync(_institutionId, _classB, "SR-B1", "R-B1");
        await _factory.QueryAsync(async db =>
        {
            (await db.Students.IgnoreQueryFilters().SingleAsync(s => s.Id == _a2)).Gender = "Male";
            db.Announcements.AddRange(
                new Announcement { Id = Guid.NewGuid(), InstitutionId = _institutionId, Title = "Sports Day", Content = "x", EffectiveDate = Today.AddDays(-1), AuthorId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow },
                new Announcement { Id = Guid.NewGuid(), InstitutionId = _institutionId, Title = "Exam Week", Content = "x", EffectiveDate = Today.AddDays(10), AuthorId = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow });
            return await db.SaveChangesAsync();
        });

        var recorder = Guid.NewGuid();
        await _factory.SeedRecordAsync(_institutionId, _a1, Today, AttendanceOutcome.Present, recorder);
        await _factory.SeedRecordAsync(_institutionId, _a2, Today, AttendanceOutcome.Late, recorder); // counts as present
        await _factory.SeedRecordAsync(_institutionId, _b1, Today, AttendanceOutcome.Leave, recorder); // not present, not in denominator
        await _factory.SeedRecordAsync(_institutionId, _a1, Today.AddDays(-1), AttendanceOutcome.Absent, recorder);
        await _factory.SeedRecordAsync(_institutionId, _teacherId, Today, AttendanceOutcome.Present, recorder, PersonType.Teacher);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(JsonElement Json, string Raw)> GetDashboardAsync(Guid userId, UserRole role, Guid? institutionId = null)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, institutionId ?? _institutionId));
        var response = await _client.GetAsync("/api/v1/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        return (JsonDocument.Parse(raw).RootElement, raw);
    }

    private static readonly string[] InstitutionWideKeys =
        ["totalStudents", "totalTeachers", "totalClasses", "presentStudentsToday", "presentTeachersToday", "averageAttendancePercent", "attendanceTrend", "studentsPerClass", "genderSplit"];

    private static void AssertNoUntracedMetrics(string raw)
    {
        Assert.DoesNotContain("satisfaction", raw, StringComparison.OrdinalIgnoreCase); // TC-609
        Assert.DoesNotContain("completion", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] // Leadership variant: institution totals, present-today, trend, per-class, gender, announcements
    public async Task Leadership_sees_the_institution_wide_dashboard()
    {
        var (d, raw) = await GetDashboardAsync(Guid.NewGuid(), UserRole.Principal);

        Assert.Equal("Leadership", d.GetProperty("variant").GetString());
        Assert.Equal(3, d.GetProperty("totalStudents").GetInt32());
        Assert.Equal(1, d.GetProperty("totalTeachers").GetInt32());
        Assert.Equal(2, d.GetProperty("totalClasses").GetInt32());
        Assert.Equal(2, d.GetProperty("presentStudentsToday").GetInt32()); // Present + Late; Leave is not present
        Assert.Equal(1, d.GetProperty("presentTeachersToday").GetInt32());

        var trend = d.GetProperty("attendanceTrend").EnumerateArray().ToList();
        Assert.Equal(7, trend.Count);
        Assert.Equal("2026-09-14", trend[^1].GetProperty("date").GetString());
        Assert.Equal(100m, trend[^1].GetProperty("studentAttendancePercent").GetDecimal()); // 2 attended / 2 counted (Leave excluded)
        Assert.Equal(0m, trend[^2].GetProperty("studentAttendancePercent").GetDecimal()); // yesterday: one Absent
        Assert.False(trend[0].TryGetProperty("studentAttendancePercent", out _)); // no data that day -> omitted, not 0

        Assert.Equal(2, d.GetProperty("studentsPerClass").EnumerateArray().Single(c => c.GetProperty("classId").GetGuid() == _classA).GetProperty("students").GetInt32());
        Assert.Equal(2, d.GetProperty("genderSplit").EnumerateArray().Count());
        Assert.Equal("Sports Day", d.GetProperty("recentAnnouncements").EnumerateArray().Single().GetProperty("title").GetString());
        Assert.Equal("Exam Week", d.GetProperty("upcomingEvents").EnumerateArray().Single().GetProperty("title").GetString());
        Assert.False(d.TryGetProperty("children", out _));
        AssertNoUntracedMetrics(raw);
    }

    [Fact] // Teacher variant — adversarial: confined to own classes, no teacher totals at all
    public async Task A_teacher_sees_only_their_own_classes_and_no_institution_totals()
    {
        var (d, raw) = await GetDashboardAsync(_teacherUserId, UserRole.Teacher);

        Assert.Equal("Teacher", d.GetProperty("variant").GetString());
        Assert.Equal(2, d.GetProperty("totalStudents").GetInt32()); // class A only, not 3
        Assert.Equal(1, d.GetProperty("totalClasses").GetInt32());
        Assert.DoesNotContain(d.GetProperty("studentsPerClass").EnumerateArray(), c => c.GetProperty("classId").GetGuid() == _classB);
        Assert.False(d.TryGetProperty("totalTeachers", out _));
        Assert.False(d.TryGetProperty("presentTeachersToday", out _));
        AssertNoUntracedMetrics(raw);
    }

    [Fact] // Student variant — BR-048 DoD, adversarial: not one institution-wide key in the raw payload
    public async Task A_student_sees_only_their_own_attendance_and_no_institution_totals()
    {
        var studentUser = await _factory.SeedAccountAsync(_institutionId, UserRole.Student, _a1);

        var (d, raw) = await GetDashboardAsync(studentUser, UserRole.Student);

        Assert.Equal("Student", d.GetProperty("variant").GetString());
        foreach (var key in InstitutionWideKeys) Assert.DoesNotContain($"\"{key}\"", raw);
        var me = d.GetProperty("children").EnumerateArray().Single();
        Assert.Equal(_a1, me.GetProperty("studentId").GetGuid());
        Assert.Equal(50m, me.GetProperty("attendancePercent").GetDecimal()); // Present today, Absent yesterday
        Assert.Equal("Present", me.GetProperty("todayOutcome").GetString());
        Assert.Single(d.GetProperty("recentAnnouncements").EnumerateArray());
        AssertNoUntracedMetrics(raw);
    }

    [Fact] // Parent variant — each linked child only; an unlinked student never appears (FR-075 / BR-066)
    public async Task A_parent_sees_each_linked_child_and_nothing_institution_wide()
    {
        await _factory.SetPlanAsync(_institutionId, SchoolSync.Infrastructure.Persistence.SchoolSyncDbContext.BespokePlanId); // CT-503: ParentAccess tier
        var parentUser = await _factory.SeedAccountAsync(_institutionId, UserRole.Parent);
        await _factory.LinkParentAsync(parentUser, _a2);
        await _factory.LinkParentAsync(parentUser, _b1);

        var (d, raw) = await GetDashboardAsync(parentUser, UserRole.Parent);

        Assert.Equal("Parent", d.GetProperty("variant").GetString());
        foreach (var key in InstitutionWideKeys) Assert.DoesNotContain($"\"{key}\"", raw);
        var children = d.GetProperty("children").EnumerateArray().ToList();
        Assert.Equal(new HashSet<Guid> { _a2, _b1 }, children.Select(c => c.GetProperty("studentId").GetGuid()).ToHashSet());
        var onLeave = children.Single(c => c.GetProperty("studentId").GetGuid() == _b1);
        Assert.False(onLeave.TryGetProperty("attendancePercent", out _)); // only Leave recorded -> no percentage, not 0%
        Assert.Equal("Leave", onLeave.GetProperty("todayOutcome").GetString());
    }

    [Fact] // BRL-048: another institution's leadership sees its own (empty) institution, never these figures
    public async Task Another_institutions_leadership_sees_none_of_this_data()
    {
        var other = await _factory.SeedInstitutionAsync("Other School");

        var (d, _) = await GetDashboardAsync(Guid.NewGuid(), UserRole.Administrator, other);

        Assert.Equal(0, d.GetProperty("totalStudents").GetInt32());
        Assert.Equal(0, d.GetProperty("totalTeachers").GetInt32());
        Assert.Empty(d.GetProperty("recentAnnouncements").EnumerateArray());
    }

    [Fact] // API-048's role list has no PlatformOperator variant; anonymous callers are 401
    public async Task Platform_operators_get_403_and_anonymous_callers_401()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(Guid.NewGuid(), UserRole.PlatformOperator, Guid.Empty));
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/dashboard")).StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/dashboard")).StatusCode);
    }

    [Fact] // API-049 — anonymous, aggregate across institutions, nothing identifying (BR-049)
    public async Task The_platform_summary_is_anonymous_aggregate_and_non_identifying()
    {
        var other = await _factory.SeedInstitutionAsync("Second Seeded School");
        var otherClass = await _factory.SeedClassAsync(other, "Grade 1", "A");
        await _factory.SeedStudentAsync(other, otherClass, "SR-O", "R-O");
        _client.DefaultRequestHeaders.Authorization = null;

        var response = await _client.GetAsync("/api/v1/platform/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var s = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(2, s.GetProperty("institutions").GetInt32());
        Assert.Equal(4, s.GetProperty("students").GetInt32()); // counts cross the tenant boundary (ADR-0006)
        Assert.Equal(3, s.GetProperty("classes").GetInt32());
        Assert.Equal(4, s.EnumerateObject().Count()); // exactly four numbers
        Assert.DoesNotContain("School", raw); // no institution name
        Assert.DoesNotContain(_institutionId.ToString(), raw); // no institution id
    }
}

// Q-223's formula, independent of any endpoint.
public class AttendanceMetricsTests
{
    [Fact]
    public void Present_and_Late_count_as_attended_Absent_counts_against_and_Leave_is_excluded() =>
        Assert.Equal(66.7m, AttendanceMetrics.Percentage([AttendanceOutcome.Present, AttendanceOutcome.Late, AttendanceOutcome.Absent, AttendanceOutcome.Leave, AttendanceOutcome.Leave]));

    [Fact]
    public void No_countable_records_is_null_not_zero()
    {
        Assert.Null(AttendanceMetrics.Percentage([]));
        Assert.Null(AttendanceMetrics.Percentage([AttendanceOutcome.Leave]));
        Assert.Equal(0m, AttendanceMetrics.Percentage([AttendanceOutcome.Absent]));
    }
}
