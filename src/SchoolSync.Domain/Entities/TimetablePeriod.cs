using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

public enum DayOfWeekSchool { Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday }

// ENT-014. 04-data-model.md §2.9.
// "room" is a plain text field, not a first-class entity (Q-208, 07-business-logic/operations.md E2).
// IAuditable: NFR-028 names schedule changes as audited mutations (02-requirements-spec/operations.md).
public class TimetablePeriod : ITenantOwned, IAuditable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;

    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public Guid TeacherId { get; set; }
    public Teacher Teacher { get; set; } = null!;

    public required string Room { get; set; } // plain text field, see class summary above

    public DayOfWeekSchool DayOfWeek { get; set; }

    // Q-225 (Rashmi Mehta, 2026-09-14): a 24h "HH:mm" start time, validated, so "9am" and "09:00"
    // can never both exist and slip past the conflict check as different slots.
    public required string TimeSlot { get; set; }

    // Q-225: FR-061 "publish". Unpublished periods are visible to leadership only (BR-056).
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }

    // BR-053/054/055 are enforced first at the application layer (decision table, all conflicts in
    // one pass) and again by three unique indexes as a second line of defense — see
    // SchoolSyncDbContext's configuration for TimetablePeriod.

    public string GetAuditEntityId() => Id.ToString();
}
