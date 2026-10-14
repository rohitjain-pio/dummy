using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Configuration;

public record ConfigCaller(Guid UserId, UserRole Role, Guid InstitutionId)
{
    public bool IsLeadership => Role is UserRole.Administrator or UserRole.Principal or UserRole.SchoolInCharge;
}

public record NotificationPreferences(bool Email, bool Sms, bool InPlatform);

public record InstitutionConfigView(
    string AcademicYear, string TimeZone, string Language,
    string GradingScale, IReadOnlyList<string> CourseCategories, IReadOnlyList<string> AssessmentTypes, string ReportCardFormat,
    NotificationPreferences Notifications, bool IsDefault);

public enum ConfigFailureReason { None, ValidationFailed, Forbidden }

public record ConfigResult(bool Succeeded, InstitutionConfigView? Value, ConfigFailureReason Failure = ConfigFailureReason.None)
{
    public static ConfigResult Ok(InstitutionConfigView value) => new(true, value);
    public static ConfigResult Fail(ConfigFailureReason reason) => new(false, null, reason);
}

// API-056–058 (CT-501). BRL-057/058/059, VAL-024/025.
public interface IInstitutionConfigService
{
    Task<InstitutionConfigView> GetAsync(ConfigCaller caller, CancellationToken ct); // API-056 GET
    Task<ConfigResult> UpdateGeneralAsync(string? academicYear, string? timeZone, string? language, ConfigCaller caller, CancellationToken ct); // API-056 PUT
    Task<ConfigResult> UpdateNotificationsAsync(NotificationPreferences? preferences, ConfigCaller caller, CancellationToken ct); // API-057
    Task<ConfigResult> UpdateAcademicAsync(string? gradingScale, IReadOnlyList<string>? courseCategories, IReadOnlyList<string>? assessmentTypes, string? reportCardFormat, ConfigCaller caller, CancellationToken ct); // API-058
}

public interface IInstitutionConfigRepository
{
    Task<InstitutionConfiguration?> FindAsync(Guid institutionId, CancellationToken ct); // tenant-filtered
    Task AddAsync(InstitutionConfiguration configuration, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
