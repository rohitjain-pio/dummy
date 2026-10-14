using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Operations;

// Q-223 (Rashmi Mehta, 2026-09-14): the one definition of "attendance percentage" every dashboard
// tile and report uses (FR-056, FR-057). Present and Late count as attended; Absent counts against;
// Leave is a plain marker (SPEC-FR-031) and is excluded from the denominator entirely.
public static class AttendanceMetrics
{
    public static bool CountsAsAttended(AttendanceOutcome o) => o is AttendanceOutcome.Present or AttendanceOutcome.Late;

    // Null when there is nothing to divide by — "no data" is not "0% attendance" (E1 / NFR-004).
    public static decimal? Percentage(IEnumerable<AttendanceOutcome> outcomes)
    {
        int attended = 0, counted = 0;
        foreach (var o in outcomes)
        {
            if (o == AttendanceOutcome.Leave) continue;
            counted++;
            if (CountsAsAttended(o)) attended++;
        }
        return counted == 0 ? null : Math.Round(100m * attended / counted, 1);
    }
}
