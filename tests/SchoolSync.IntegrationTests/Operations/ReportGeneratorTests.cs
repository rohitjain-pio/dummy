using System.Text;
using SchoolSync.Application.Operations;
using SchoolSync.Domain.Entities;
using Xunit;

namespace SchoolSync.IntegrationTests.Operations;

// CT-402 generation rules. TC-601/602 ("computed solely from AttendanceRecord") are proved two ways:
// the numbers match Q-223's formulas, and a spy data source records exactly which sources were read.
public class ReportGeneratorTests
{
    private static readonly Guid ClassA = Guid.NewGuid(), ClassB = Guid.NewGuid();
    private static readonly Guid Asha = Guid.NewGuid(), Ravi = Guid.NewGuid(), Meena = Guid.NewGuid();
    private static readonly Guid TeacherUser = Guid.NewGuid();

    private sealed class SpyDataSource : IReportDataSource
    {
        public readonly List<string> Calls = [];
        public string? TimeZone = "Asia/Kolkata";
        public List<ClassLabel> Classes = [new(ClassA, "Grade 5", "A"), new(ClassB, "Grade 6", "B")];
        public List<StudentLabel> Students = [new(Asha, "Asha", "SR-1", ClassA), new(Ravi, "Ravi, Jr.", "SR-2", ClassA), new(Meena, "Meena", "SR-3", ClassB)];
        public List<SubjectClasses> Subjects = [];
        public List<TeacherLabel> Teachers = [];
        public List<AttendanceRow> Attendance = [];

        public Task<string?> InstitutionTimeZoneAsync(CancellationToken ct) { Calls.Add("TimeZone"); return Task.FromResult(TimeZone); }
        public Task<IReadOnlyList<ClassLabel>> ClassesAsync(IReadOnlyCollection<Guid>? only, CancellationToken ct) { Calls.Add("Classes"); return Task.FromResult<IReadOnlyList<ClassLabel>>(Classes.Where(c => only == null || only.Contains(c.Id)).ToList()); }
        public Task<IReadOnlyList<StudentLabel>> StudentsAsync(IReadOnlyCollection<Guid>? only, CancellationToken ct) { Calls.Add("Students"); return Task.FromResult<IReadOnlyList<StudentLabel>>(Students.Where(s => only == null || (s.ClassId is { } c && only.Contains(c))).ToList()); }
        public Task<IReadOnlyList<SubjectClasses>> SubjectsAsync(IReadOnlyCollection<Guid>? only, CancellationToken ct) { Calls.Add("Subjects"); return Task.FromResult<IReadOnlyList<SubjectClasses>>(Subjects); }
        public Task<IReadOnlyList<TeacherLabel>> TeachersAsync(Guid? onlyUser, Guid? onlyClass, CancellationToken ct) { Calls.Add("Teachers"); return Task.FromResult<IReadOnlyList<TeacherLabel>>(Teachers.Where(t => onlyUser == null || t.UserAccountId == onlyUser).ToList()); }
        public Task<IReadOnlyList<AttendanceRow>> StudentAttendanceAsync(IReadOnlyCollection<Guid> ids, DateOnly from, DateOnly to, CancellationToken ct) { Calls.Add("StudentAttendance"); return Task.FromResult<IReadOnlyList<AttendanceRow>>(Attendance.Where(a => ids.Contains(a.PersonId) && a.Date >= from && a.Date <= to).ToList()); }
    }

    private static ReportRun Run(ReportType type, DateOnly from, DateOnly to, Guid? classId = null, Guid? teacherUser = null) => new()
    {
        Id = Guid.NewGuid(),
        ReportType = type,
        Format = ReportFormat.Csv,
        FromDate = from,
        ToDate = to,
        ScopeClassId = classId,
        ScopeTeacherUserId = teacherUser,
        Status = ReportRunStatus.Pending
    };

    private static AttendanceRow Row(Guid person, DateOnly date, AttendanceOutcome o, Guid? by = null, DateTime? recordedUtc = null) =>
        new(person, date, o, by ?? Guid.NewGuid(), recordedUtc ?? date.ToDateTime(new TimeOnly(4, 0), DateTimeKind.Utc));

    private static List<string> Lines(ReportGenerator.Output output) =>
        Encoding.UTF8.GetString(output.Content).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(l => l.Split('\n', StringSplitOptions.RemoveEmptyEntries)).Select(l => l.TrimEnd('\r')).ToList();

    private static readonly DateOnly Mon = new(2026, 9, 7), Tue = new(2026, 9, 8), Fri = new(2026, 9, 11), Sun = new(2026, 9, 13);

    [Fact] // TC-601 — adversarial: reads nothing but student identity + attendance, and the % is Q-223's
    public async Task Student_performance_is_computed_solely_from_attendance_records()
    {
        var data = new SpyDataSource();
        data.Attendance = [Row(Asha, Mon, AttendanceOutcome.Present), Row(Asha, Tue, AttendanceOutcome.Late), Row(Asha, Fri, AttendanceOutcome.Absent), Row(Asha, Sun, AttendanceOutcome.Leave)];

        var lines = Lines(await new ReportGenerator(data).GenerateAsync(Run(ReportType.StudentPerformance, Mon, Sun, ClassA), CancellationToken.None));

        Assert.Equal(["Students", "StudentAttendance"], data.Calls); // no subject, teacher or any other source
        Assert.Equal("Student,SrNumber,Present,Late,Absent,Leave,AttendancePercent", lines[0]);
        Assert.Contains("Asha,SR-1,1,1,1,1,66.7", lines); // (P+L)/(P+L+A), Leave excluded
        Assert.Contains("\"Ravi, Jr.\",SR-2,0,0,0,0,", lines); // no records -> blank %, not 0; comma escaped
        Assert.DoesNotContain(lines, l => l.StartsWith("Meena")); // class B is out of scope
    }

    [Fact] // TC-602 — adversarial: teacher performance = compliance + timeliness from attendance only
    public async Task Teacher_performance_is_compliance_and_timeliness_from_attendance_records_only()
    {
        var data = new SpyDataSource { Teachers = [new(Guid.NewGuid(), "Meera Rao", TeacherUser, [ClassA])] };
        data.Attendance =
        [
            // Mon: marked on the day (09:30 IST); Tue: marked the next day (late); Fri: not marked.
            Row(Asha, Mon, AttendanceOutcome.Present, TeacherUser, new DateTime(2026, 9, 7, 4, 0, 0, DateTimeKind.Utc)),
            Row(Asha, Tue, AttendanceOutcome.Present, TeacherUser, new DateTime(2026, 9, 9, 4, 0, 0, DateTimeKind.Utc)),
            // Sunday records don't create an expected marking day.
            Row(Asha, Sun, AttendanceOutcome.Present, Guid.NewGuid()),
        ];

        var lines = Lines(await new ReportGenerator(data).GenerateAsync(Run(ReportType.TeacherPerformance, Mon, Sun), CancellationToken.None));

        Assert.DoesNotContain("Subjects", data.Calls);
        Assert.DoesNotContain("Classes", data.Calls);
        Assert.Equal("Teacher,ExpectedMarkingDays,DaysMarked,CompliancePercent,RecordsMarked,RecordsOnTime,TimelinessPercent", lines[0]);
        // 1 class × 5 weekdays = 5 expected; Mon + Tue marked = 2 -> 40%. Own records 2, on time 1 -> 50%.
        Assert.Equal("Meera Rao,5,2,40.0,2,1,50.0", lines[1]);
    }

    [Fact] // Timeliness uses the institution's local date, not UTC
    public async Task A_record_made_just_after_local_midnight_UTC_previous_day_is_not_on_time()
    {
        var data = new SpyDataSource { Teachers = [new(Guid.NewGuid(), "Meera Rao", TeacherUser, [ClassA])] };
        // Attendance for Mon, recorded 2026-09-07 19:00 UTC = 2026-09-08 00:30 IST: the next local day.
        data.Attendance = [Row(Asha, Mon, AttendanceOutcome.Present, TeacherUser, new DateTime(2026, 9, 7, 19, 0, 0, DateTimeKind.Utc))];

        var lines = Lines(await new ReportGenerator(data).GenerateAsync(Run(ReportType.TeacherPerformance, Mon, Mon), CancellationToken.None));

        Assert.Equal("Meera Rao,1,1,100.0,1,0,0.0", lines[1]);
    }

    [Fact] // Class attendance and course analytics use the same Q-223 percentage
    public async Task Class_attendance_and_course_analytics_aggregate_by_class_and_subject()
    {
        var data = new SpyDataSource { Subjects = [new(Guid.NewGuid(), "Science", [ClassA, ClassB])] };
        data.Attendance = [Row(Asha, Mon, AttendanceOutcome.Present), Row(Ravi, Mon, AttendanceOutcome.Absent), Row(Meena, Mon, AttendanceOutcome.Present)];

        var classLines = Lines(await new ReportGenerator(data).GenerateAsync(Run(ReportType.ClassAttendance, Mon, Mon), CancellationToken.None));
        var courseLines = Lines(await new ReportGenerator(data).GenerateAsync(Run(ReportType.CourseAnalytics, Mon, Mon), CancellationToken.None));

        Assert.Contains("Grade 5,A,2,1,1,0,1,0,50.0", classLines);
        Assert.Contains("Grade 6,B,1,1,1,0,0,0,100.0", classLines);
        Assert.Equal("Science,2,75.0", courseLines[1]); // average of 50 and 100
    }

    [Fact] // E1: an empty scope still generates — header only, not an error
    public async Task A_report_with_no_data_is_generated_with_just_its_header()
    {
        var data = new SpyDataSource { Classes = [], Students = [] };

        var lines = Lines(await new ReportGenerator(data).GenerateAsync(Run(ReportType.ClassAttendance, Mon, Sun), CancellationToken.None));

        Assert.Single(lines);
    }

    [Fact] // TC-609 — adversarial: no Satisfaction/Completion report type or column exists anywhere
    public async Task No_report_type_or_column_carries_satisfaction_or_completion()
    {
        Assert.DoesNotContain(Enum.GetNames<ReportType>(), n => n.Contains("Satisfaction") || n.Contains("Completion"));
        foreach (var type in Enum.GetValues<ReportType>())
        {
            var header = Lines(await new ReportGenerator(new SpyDataSource()).GenerateAsync(Run(type, Mon, Sun), CancellationToken.None))[0];
            Assert.DoesNotContain("satisfaction", header, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("completion", header, StringComparison.OrdinalIgnoreCase);
        }
    }
}
