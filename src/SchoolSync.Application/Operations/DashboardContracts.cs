using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Operations;

public record OperationsCaller(Guid UserId, UserRole Role, Guid InstitutionId)
{
    public bool IsLeadership => Role is UserRole.Administrator or UserRole.Principal or UserRole.SchoolInCharge;
}

public enum DashboardVariant { Leadership, Teacher, Student, Parent }

public record TrendPoint(DateOnly Date, decimal? StudentAttendancePercent, decimal? TeacherAttendancePercent);
public record ClassCount(Guid ClassId, string Name, string Section, int Students);
public record GenderCount(string Gender, int Students);
public record AnnouncementSummary(Guid Id, string Title, DateOnly EffectiveDate);
public record ChildAttendance(Guid StudentId, string Name, decimal? AttendancePercent, AttendanceOutcome? TodayOutcome);

// Q-224. One record with role-conditional members: members a variant must not see are null (and
// omitted from JSON), never zero — so "no institution totals for Student/Parent" (BR-048) is a
// property of the payload, not of what a client chooses to render.
public record DashboardResult(
    DashboardVariant Variant,
    DateOnly Today,
    int? TotalStudents,
    int? TotalTeachers,
    int? TotalClasses,
    int? PresentStudentsToday,
    int? PresentTeachersToday,
    decimal? AverageAttendancePercent,
    IReadOnlyList<TrendPoint>? AttendanceTrend,
    IReadOnlyList<ClassCount>? StudentsPerClass,
    IReadOnlyList<GenderCount>? GenderSplit,
    IReadOnlyList<ChildAttendance>? Children,
    IReadOnlyList<AnnouncementSummary> RecentAnnouncements,
    IReadOnlyList<AnnouncementSummary> UpcomingEvents);

public record PlatformSummary(int Institutions, int Students, int Teachers, int Classes);

public interface IDashboardService
{
    Task<DashboardResult?> GetAsync(OperationsCaller caller, CancellationToken ct); // API-048; null = no variant for this role
    Task<PlatformSummary> GetPlatformSummaryAsync(CancellationToken ct); // API-049
}

public record AttendanceFact(Guid PersonId, PersonType PersonType, DateOnly Date, AttendanceOutcome Outcome);

// Tenant-filtered except the one method named ...AcrossInstitutions (ADR-0006).
public interface IDashboardRepository
{
    Task<string?> FindInstitutionTimeZoneAsync(Guid institutionId, CancellationToken ct);
    Task<Guid?> FindTeacherIdForUserAsync(Guid userId, CancellationToken ct);
    Task<Guid?> FindStudentIdForUserAsync(Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> FindLinkedStudentIdsAsync(Guid parentUserId, CancellationToken ct);

    Task<IReadOnlyList<Class>> ListClassesAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct);
    Task<IReadOnlyList<Guid>> ListTaughtClassIdsAsync(Guid teacherId, CancellationToken ct); // class teacher or subject mapping
    Task<IReadOnlyList<Student>> ListActiveStudentsAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct);
    Task<IReadOnlyList<Student>> FindStudentsAsync(IReadOnlyCollection<Guid> studentIds, CancellationToken ct);
    Task<int> CountActiveTeachersAsync(CancellationToken ct);
    Task<IReadOnlyList<AttendanceFact>> ListAttendanceAsync(IReadOnlyCollection<Guid>? onlyPersonIds, PersonType? personType, DateOnly? from, DateOnly to, CancellationToken ct);
    Task<IReadOnlyList<Announcement>> ListRecentAnnouncementsAsync(DateOnly today, int take, CancellationToken ct);
    Task<IReadOnlyList<Announcement>> ListUpcomingAnnouncementsAsync(DateOnly today, int take, CancellationToken ct);

    Task<PlatformSummary> CountAcrossInstitutionsAsync(CancellationToken ct); // ADR-0006
}
