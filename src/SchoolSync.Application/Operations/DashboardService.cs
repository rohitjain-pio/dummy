using SchoolSync.Application.Attendance;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Operations;

// API-048/049 (CT-401). BRL-048/049/050, FR-052/053/056/057/074/075, UI-015, Q-224.
// Computed live on every request (Q-224: no A2 cache this release; NFR-007 is measured in CT-701).
public class DashboardService(IDashboardRepository repository, TimeProvider clock) : IDashboardService
{
    public const int TrendDays = 7; // Q-224
    public const int AnnouncementsShown = 5; // Q-224

    public async Task<DashboardResult?> GetAsync(OperationsCaller caller, CancellationToken ct)
    {
        // BRL-050: "today" is the institution's calendar date, via the same calendar attendance uses.
        var zone = AttendanceCalendar.ResolveTimeZone(await repository.FindInstitutionTimeZoneAsync(caller.InstitutionId, ct), out _);
        var today = AttendanceCalendar.LocalToday(clock.GetUtcNow(), zone);

        var recent = Summaries(await repository.ListRecentAnnouncementsAsync(today, AnnouncementsShown, ct));
        var upcoming = Summaries(await repository.ListUpcomingAnnouncementsAsync(today, AnnouncementsShown, ct));

        if (caller.IsLeadership) return await InstitutionViewAsync(DashboardVariant.Leadership, null, today, recent, upcoming, ct);

        switch (caller.Role)
        {
            case UserRole.Teacher:
                var teacherId = await repository.FindTeacherIdForUserAsync(caller.UserId, ct);
                var own = teacherId is null ? [] : await repository.ListTaughtClassIdsAsync(teacherId.Value, ct);
                return await InstitutionViewAsync(DashboardVariant.Teacher, own, today, recent, upcoming, ct);

            case UserRole.Student:
                var studentId = await repository.FindStudentIdForUserAsync(caller.UserId, ct);
                return await PersonalViewAsync(DashboardVariant.Student, studentId is null ? [] : [studentId.Value], today, recent, upcoming, ct);

            case UserRole.Parent:
                return await PersonalViewAsync(DashboardVariant.Parent, await repository.FindLinkedStudentIdsAsync(caller.UserId, ct), today, recent, upcoming, ct);

            default:
                return null; // PlatformOperator has no institution dashboard (API-048's role list)
        }
    }

    private static List<AnnouncementSummary> Summaries(IEnumerable<Announcement> a) =>
        a.Select(x => new AnnouncementSummary(x.Id, x.Title, x.EffectiveDate)).ToList();

    // Leadership: the whole institution. Teacher: the same shape confined to the classes they
    // teach, with no teacher totals at all (UI-015: "no institution-wide totals").
    private async Task<DashboardResult> InstitutionViewAsync(
        DashboardVariant variant, IReadOnlyCollection<Guid>? onlyClassIds, DateOnly today,
        List<AnnouncementSummary> recent, List<AnnouncementSummary> upcoming, CancellationToken ct)
    {
        var isLeadership = variant == DashboardVariant.Leadership;
        var classes = await repository.ListClassesAsync(onlyClassIds, ct);
        var students = await repository.ListActiveStudentsAsync(onlyClassIds, ct);
        var studentIds = students.Select(s => s.Id).ToList();
        var from = today.AddDays(-(TrendDays - 1));

        var studentFacts = await repository.ListAttendanceAsync(studentIds, PersonType.Student, from, today, ct);
        var teacherFacts = isLeadership
            ? await repository.ListAttendanceAsync(null, PersonType.Teacher, from, today, ct)
            : [];

        var trend = Enumerable.Range(0, TrendDays).Select(i => from.AddDays(i)).Select(day => new TrendPoint(
            day,
            AttendanceMetrics.Percentage(studentFacts.Where(f => f.Date == day).Select(f => f.Outcome)),
            isLeadership ? AttendanceMetrics.Percentage(teacherFacts.Where(f => f.Date == day).Select(f => f.Outcome)) : null)).ToList();

        return new DashboardResult(
            variant, today,
            TotalStudents: students.Count,
            TotalTeachers: isLeadership ? await repository.CountActiveTeachersAsync(ct) : null,
            TotalClasses: classes.Count,
            PresentStudentsToday: studentFacts.Count(f => f.Date == today && AttendanceMetrics.CountsAsAttended(f.Outcome)),
            PresentTeachersToday: isLeadership ? teacherFacts.Count(f => f.Date == today && AttendanceMetrics.CountsAsAttended(f.Outcome)) : null,
            AverageAttendancePercent: AttendanceMetrics.Percentage(studentFacts.Select(f => f.Outcome)),
            AttendanceTrend: trend,
            StudentsPerClass: classes.OrderBy(c => c.Name).ThenBy(c => c.Section)
                .Select(c => new ClassCount(c.Id, c.Name, c.Section, students.Count(s => s.ClassId == c.Id))).ToList(),
            GenderSplit: students.GroupBy(s => s.Gender).OrderBy(g => g.Key).Select(g => new GenderCount(g.Key, g.Count())).ToList(),
            Children: null,
            RecentAnnouncements: recent,
            UpcomingEvents: upcoming);
    }

    // Student: their own record. Parent: each linked child (FR-075). Never an institution figure.
    private async Task<DashboardResult> PersonalViewAsync(
        DashboardVariant variant, IReadOnlyCollection<Guid> studentIds, DateOnly today,
        List<AnnouncementSummary> recent, List<AnnouncementSummary> upcoming, CancellationToken ct)
    {
        var students = studentIds.Count == 0 ? [] : await repository.FindStudentsAsync(studentIds, ct);
        var facts = studentIds.Count == 0 ? [] : await repository.ListAttendanceAsync(studentIds, PersonType.Student, null, today, ct);

        var children = students.OrderBy(s => s.Name).Select(s =>
        {
            var own = facts.Where(f => f.PersonId == s.Id).ToList();
            return new ChildAttendance(s.Id, s.Name, AttendanceMetrics.Percentage(own.Select(f => f.Outcome)),
                own.FirstOrDefault(f => f.Date == today)?.Outcome);
        }).ToList();

        return new DashboardResult(variant, today, null, null, null, null, null, null, null, null, null, children, recent, upcoming);
    }

    public Task<PlatformSummary> GetPlatformSummaryAsync(CancellationToken ct) => repository.CountAcrossInstitutionsAsync(ct);
}
