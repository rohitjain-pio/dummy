using System.Globalization;
using System.Text;
using SchoolSync.Application.Attendance;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Operations;

// PF-010 step 2 (CT-402): computes one report from its run parameters and renders it as CSV
// (Q-226). Every metric uses Q-223's definitions via AttendanceMetrics; nothing but attendance
// records feeds a number.
public class ReportGenerator(IReportDataSource data)
{
    public record Output(string FileName, string ContentType, byte[] Content);

    public async Task<Output> GenerateAsync(ReportRun run, CancellationToken ct)
    {
        IReadOnlyCollection<Guid>? classScope = run.ScopeClassId is { } c ? [c] : null;
        var (header, rows) = run.ReportType switch
        {
            ReportType.ClassAttendance => await ClassAttendanceAsync(run, classScope, ct),
            ReportType.StudentPerformance => await StudentPerformanceAsync(run, classScope, ct),
            ReportType.CourseAnalytics => await CourseAnalyticsAsync(run, classScope, ct),
            ReportType.TeacherPerformance => await TeacherPerformanceAsync(run, ct),
            _ => throw new InvalidOperationException($"Unknown report type {run.ReportType}.")
        };

        // E1: an empty scope still produces a report — the header row alone, not an error.
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', header.Select(Escape)));
        foreach (var row in rows) csv.AppendLine(string.Join(',', row.Select(Escape)));

        var fileName = $"{run.ReportType}-{run.FromDate:yyyyMMdd}-{run.ToDate:yyyyMMdd}.csv";
        return new Output(fileName, "text/csv", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray());
    }

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static string Pct(decimal? p) => p?.ToString("0.0", CultureInfo.InvariantCulture) ?? "";

    private static (int P, int L, int A, int Lv) Tally(IEnumerable<AttendanceRow> rows)
    {
        int p = 0, l = 0, a = 0, lv = 0;
        foreach (var r in rows)
        {
            switch (r.Outcome)
            {
                case AttendanceOutcome.Present: p++; break;
                case AttendanceOutcome.Late: l++; break;
                case AttendanceOutcome.Absent: a++; break;
                case AttendanceOutcome.Leave: lv++; break;
            }
        }
        return (p, l, a, lv);
    }

    private async Task<(string[], List<string[]>)> ClassAttendanceAsync(ReportRun run, IReadOnlyCollection<Guid>? scope, CancellationToken ct)
    {
        var classes = await data.ClassesAsync(scope, ct);
        var students = await data.StudentsAsync(scope, ct);
        var attendance = await data.StudentAttendanceAsync(students.Select(s => s.Id).ToList(), run.FromDate, run.ToDate, ct);

        var rows = classes.OrderBy(c => c.Name).ThenBy(c => c.Section).Select(c =>
        {
            var ids = students.Where(s => s.ClassId == c.Id).Select(s => s.Id).ToHashSet();
            var own = attendance.Where(r => ids.Contains(r.PersonId)).ToList();
            var t = Tally(own);
            return new[]
            {
                c.Name, c.Section, ids.Count.ToString(CultureInfo.InvariantCulture), own.Select(r => r.Date).Distinct().Count().ToString(CultureInfo.InvariantCulture),
                t.P.ToString(CultureInfo.InvariantCulture), t.L.ToString(CultureInfo.InvariantCulture), t.A.ToString(CultureInfo.InvariantCulture), t.Lv.ToString(CultureInfo.InvariantCulture),
                Pct(AttendanceMetrics.Percentage(own.Select(r => r.Outcome)))
            };
        }).ToList();
        return (["Class", "Section", "Students", "RecordedDays", "Present", "Late", "Absent", "Leave", "AttendancePercent"], rows);
    }

    // FR-057 / BRL-090: attendance percentage ONLY.
    private async Task<(string[], List<string[]>)> StudentPerformanceAsync(ReportRun run, IReadOnlyCollection<Guid>? scope, CancellationToken ct)
    {
        var students = await data.StudentsAsync(scope, ct);
        var attendance = await data.StudentAttendanceAsync(students.Select(s => s.Id).ToList(), run.FromDate, run.ToDate, ct);

        var rows = students.OrderBy(s => s.Name).Select(s =>
        {
            var own = attendance.Where(r => r.PersonId == s.Id).ToList();
            var t = Tally(own);
            return new[]
            {
                s.Name, s.SrNumber, t.P.ToString(CultureInfo.InvariantCulture), t.L.ToString(CultureInfo.InvariantCulture),
                t.A.ToString(CultureInfo.InvariantCulture), t.Lv.ToString(CultureInfo.InvariantCulture), Pct(AttendanceMetrics.Percentage(own.Select(r => r.Outcome)))
            };
        }).ToList();
        return (["Student", "SrNumber", "Present", "Late", "Absent", "Leave", "AttendancePercent"], rows);
    }

    // Q-223: per subject, the average of the attendance % of the classes mapped to it.
    private async Task<(string[], List<string[]>)> CourseAnalyticsAsync(ReportRun run, IReadOnlyCollection<Guid>? scope, CancellationToken ct)
    {
        var subjects = await data.SubjectsAsync(scope, ct);
        var students = await data.StudentsAsync(scope, ct);
        var attendance = await data.StudentAttendanceAsync(students.Select(s => s.Id).ToList(), run.FromDate, run.ToDate, ct);

        decimal? ClassPct(Guid classId)
        {
            var ids = students.Where(s => s.ClassId == classId).Select(s => s.Id).ToHashSet();
            return AttendanceMetrics.Percentage(attendance.Where(r => ids.Contains(r.PersonId)).Select(r => r.Outcome));
        }

        var rows = subjects.OrderBy(s => s.SubjectName).Select(s =>
        {
            var pcts = s.ClassIds.Select(ClassPct).Where(p => p is not null).Select(p => p!.Value).ToList();
            decimal? avg = pcts.Count == 0 ? null : Math.Round(pcts.Average(), 1);
            return new[] { s.SubjectName, s.ClassIds.Count.ToString(CultureInfo.InvariantCulture), Pct(avg) };
        }).ToList();
        return (["Subject", "Classes", "AverageAttendancePercent"], rows);
    }

    // FR-058 / BRL-091, Q-223: compliance = share of (own class × Mon–Fri day in range) with any
    // record; timeliness = share of the teacher's own records marked on the attendance date itself
    // in the institution's time zone. Nothing but attendance records feeds either number.
    private async Task<(string[], List<string[]>)> TeacherPerformanceAsync(ReportRun run, CancellationToken ct)
    {
        var zone = AttendanceCalendar.ResolveTimeZone(await data.InstitutionTimeZoneAsync(ct), out _);
        var teachers = await data.TeachersAsync(run.ScopeTeacherUserId, run.ScopeClassId, ct);
        var allClassIds = teachers.SelectMany(t => t.ClassIds).Distinct().ToList();
        var students = await data.StudentsAsync(allClassIds, ct);
        var attendance = await data.StudentAttendanceAsync(students.Select(s => s.Id).ToList(), run.FromDate, run.ToDate, ct);

        var weekdays = Enumerable.Range(0, run.ToDate.DayNumber - run.FromDate.DayNumber + 1)
            .Select(run.FromDate.AddDays)
            .Where(d => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToList();

        var rows = teachers.OrderBy(t => t.Name).Select(t =>
        {
            var expected = t.ClassIds.Count * weekdays.Count;
            var marked = t.ClassIds.Sum(classId =>
            {
                var ids = students.Where(s => s.ClassId == classId).Select(s => s.Id).ToHashSet();
                var days = attendance.Where(r => ids.Contains(r.PersonId)).Select(r => r.Date).ToHashSet();
                return weekdays.Count(days.Contains);
            });

            var ownRecords = t.UserAccountId is { } uid ? attendance.Where(r => r.RecordedByUserId == uid).ToList() : [];
            var onTime = ownRecords.Count(r => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.RecordedAtUtc, DateTimeKind.Utc), zone)) == r.Date);

            return new[]
            {
                t.Name, expected.ToString(CultureInfo.InvariantCulture), marked.ToString(CultureInfo.InvariantCulture),
                Pct(expected == 0 ? null : Math.Round(100m * marked / expected, 1)),
                ownRecords.Count.ToString(CultureInfo.InvariantCulture), onTime.ToString(CultureInfo.InvariantCulture),
                Pct(ownRecords.Count == 0 ? null : Math.Round(100m * onTime / ownRecords.Count, 1))
            };
        }).ToList();
        return (["Teacher", "ExpectedMarkingDays", "DaysMarked", "CompliancePercent", "RecordsMarked", "RecordsOnTime", "TimelinessPercent"], rows);
    }
}
