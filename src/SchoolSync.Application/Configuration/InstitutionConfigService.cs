using System.Text.Json;
using SchoolSync.Application.Attendance;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Configuration;

// API-056–058 (CT-501). One configuration row per institution (§2.11), created on first write.
// BRL-059: every change is audited by the CT-003 interceptor (InstitutionConfiguration is
// IAuditable) — unconditionally. There is no request field that can turn auditing off, even though
// the mockup's configure modal shows such a checkbox (ui-design-final.md §12).
public class InstitutionConfigService(IInstitutionConfigRepository repository) : IInstitutionConfigService
{
    // VAL-025 — PROVISIONAL, Q-209: English only this release; the field is single-valued now so a
    // later list needs no schema change.
    public static readonly IReadOnlySet<string> SupportedLanguages = new HashSet<string>(StringComparer.Ordinal) { "en" };

    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly NotificationPreferences DefaultNotifications = new(Email: true, Sms: false, InPlatform: true);

    // VAL-024: a real IANA identifier. TryConvertIanaIdToWindowsId is true only for IANA ids, so a
    // Windows id such as "India Standard Time" is refused even on a Windows host that could resolve it.
    public static bool IsIanaTimeZone(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out _) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);

    private static InstitutionConfigView ToView(InstitutionConfiguration? c) => c is null
        ? new InstitutionConfigView("", AttendanceCalendar.DefaultTimeZoneId, "en", "", [], [], "", DefaultNotifications, IsDefault: true)
        : new InstitutionConfigView(c.AcademicYear, c.TimeZone, c.Language, c.GradingScale,
            Deserialize<List<string>>(c.CourseCategories) ?? [], Deserialize<List<string>>(c.AssessmentTypes) ?? [], c.ReportCardFormat,
            Deserialize<NotificationPreferences>(c.NotificationPreferencesJson) ?? DefaultNotifications, IsDefault: false);

    private static T? Deserialize<T>(string value)
    {
        try { return string.IsNullOrWhiteSpace(value) ? default : JsonSerializer.Deserialize<T>(value, Json); }
        catch (JsonException) { return default; }
    }

    public async Task<InstitutionConfigView> GetAsync(ConfigCaller caller, CancellationToken ct) =>
        ToView(await repository.FindAsync(caller.InstitutionId, ct)); // BRL-057: tenant-filtered read of the caller's own row

    private async Task<InstitutionConfiguration> FindOrCreateAsync(Guid institutionId, CancellationToken ct)
    {
        if (await repository.FindAsync(institutionId, ct) is { } existing) return existing;
        var created = new InstitutionConfiguration
        {
            InstitutionId = institutionId,
            AcademicYear = "",
            TimeZone = AttendanceCalendar.DefaultTimeZoneId,
            Language = "en",
            GradingScale = "",
            CourseCategories = "[]",
            AssessmentTypes = "[]",
            ReportCardFormat = "",
            NotificationPreferencesJson = JsonSerializer.Serialize(DefaultNotifications, Json)
        };
        await repository.AddAsync(created, ct);
        return created;
    }

    private async Task<ConfigResult> WriteAsync(ConfigCaller caller, Func<InstitutionConfiguration, bool> apply, CancellationToken ct)
    {
        if (!caller.IsLeadership) return ConfigResult.Fail(ConfigFailureReason.Forbidden); // BRL-058
        var config = await FindOrCreateAsync(caller.InstitutionId, ct); // BRL-057: the caller's own institution only
        if (!apply(config)) return ConfigResult.Fail(ConfigFailureReason.ValidationFailed);
        await repository.SaveChangesAsync(ct); // BRL-059: effective immediately; audited by the interceptor
        return ConfigResult.Ok(ToView(config));
    }

    public Task<ConfigResult> UpdateGeneralAsync(string? academicYear, string? timeZone, string? language, ConfigCaller caller, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(academicYear) || !IsIanaTimeZone(timeZone) || language is null || !SupportedLanguages.Contains(language))
            return Task.FromResult(caller.IsLeadership ? ConfigResult.Fail(ConfigFailureReason.ValidationFailed) : ConfigResult.Fail(ConfigFailureReason.Forbidden));

        return WriteAsync(caller, c =>
        {
            c.AcademicYear = academicYear.Trim();
            c.TimeZone = timeZone!;
            c.Language = language;
            return true;
        }, ct);
    }

    public Task<ConfigResult> UpdateNotificationsAsync(NotificationPreferences? preferences, ConfigCaller caller, CancellationToken ct) =>
        WriteAsync(caller, c =>
        {
            if (preferences is null) return false;
            c.NotificationPreferencesJson = JsonSerializer.Serialize(preferences, Json);
            return true;
        }, ct);

    public Task<ConfigResult> UpdateAcademicAsync(string? gradingScale, IReadOnlyList<string>? courseCategories, IReadOnlyList<string>? assessmentTypes,
        string? reportCardFormat, ConfigCaller caller, CancellationToken ct) =>
        WriteAsync(caller, c =>
        {
            if (gradingScale is null || courseCategories is null || assessmentTypes is null || reportCardFormat is null
                || courseCategories.Any(string.IsNullOrWhiteSpace) || assessmentTypes.Any(string.IsNullOrWhiteSpace))
                return false;
            c.GradingScale = gradingScale.Trim();
            c.CourseCategories = JsonSerializer.Serialize(courseCategories.Select(x => x.Trim()).ToList(), Json);
            c.AssessmentTypes = JsonSerializer.Serialize(assessmentTypes.Select(x => x.Trim()).ToList(), Json);
            c.ReportCardFormat = reportCardFormat.Trim();
            return true;
        }, ct);
}
