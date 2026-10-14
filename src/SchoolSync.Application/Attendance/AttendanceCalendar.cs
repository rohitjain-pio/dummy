namespace SchoolSync.Application.Attendance;

// The attendance day, per 07-business-logic/attendance.md "Guards, precisely": "same day" means
// today == attendance_date in the INSTITUTION's time zone (FR-063), never UTC. Every day-count the
// correction/lock state machine uses goes through here, so the boundary is computed one way only.
public static class AttendanceCalendar
{
    // CFG-004 — fixed platform constant, never read from InstitutionConfiguration
    // (constitution.md §2.11). Locked means MORE than 30 calendar days: day 30 is still
    // correctable (Q-214, Rashmi Mehta, 2026-09-14; TC-408's ">30 not >=30").
    public const int LockAfterDays = 30;

    // Q-215 (Rashmi Mehta, 2026-09-14): InstitutionConfiguration.TimeZone is an IANA id. When an
    // institution has no configuration row yet (config CRUD is CT-501, P5) or the id does not
    // resolve, this single platform default applies. Proposed as CFG-009 — not yet in
    // 12-infrastructure-spec.md, recorded in _upstream-feedback.md. Named constant for the same
    // reason CFG-005 is one in ParentLinkCodeGenerator.
    public const string DefaultTimeZoneId = "Asia/Kolkata";

    public static TimeZoneInfo ResolveTimeZone(string? configuredId, out bool usedFallback)
    {
        if (!string.IsNullOrWhiteSpace(configuredId) && TimeZoneInfo.TryFindSystemTimeZoneById(configuredId, out var zone))
        {
            usedFallback = false;
            return zone;
        }

        usedFallback = true;
        return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
    }

    public static DateOnly LocalToday(DateTimeOffset utcNow, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);

    // Whole calendar days from attendanceDate to today. Negative means a future date.
    public static int DaysSince(DateOnly attendanceDate, DateOnly today) =>
        today.DayNumber - attendanceDate.DayNumber;

    public static bool IsLocked(int daysSince) => daysSince > LockAfterDays;
}
