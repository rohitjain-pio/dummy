namespace SchoolSync.Domain.Common;

// 07-business-logic/_shared.md §3 (D9, NFR-028-033, BR-039, BR-044, BR-059): governed entities —
// NFR-028 names student/staff/class/attendance/announcement explicitly; BR-044 (message
// revision/withdrawal) and BR-059 (institution configuration changes) extend the set by
// citation. Marking an entity with this interface is the ONLY thing a future CT needs to do to
// get its mutations audited — the SaveChanges interceptor discovers governed entities this way,
// not via a hand-maintained type list that would need editing every time a new CT adds one.
public interface IAuditable
{
    // Returns a stable identifier for AuditLog.entity_id — a Guid Id property on every governed
    // entity so far, but expressed as a method rather than an assumed property name/type in case
    // a future governed entity has a composite key.
    string GetAuditEntityId();
}
