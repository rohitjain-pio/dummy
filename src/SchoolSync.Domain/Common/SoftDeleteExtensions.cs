namespace SchoolSync.Domain.Common;

// BR-005/D8 (07-business-logic/_shared.md §2): the one canonical way any of the five soft-
// deletable entities (Institution, Student, Teacher, Room, UserAccount) transitions state. Every
// future CT that adds a "remove"/"withdraw"/"deactivate" endpoint for one of these five must call
// through here, not hand-roll `entity.IsActive = false` inline — that is what makes "Reactivate
// mirrors Deactivate exactly" a property of the code rather than a convention someone has to
// remember to honor consistently across five different feature CTs.
public static class SoftDeleteExtensions
{
    // No guard here (07-business-logic/_shared.md §2: "no guard required at the entity level" —
    // deactivation is always permitted; feature-level guards like BR-025's teacher-removal check
    // are layered on top by the calling CT, before this is ever invoked).
    public static void Deactivate(this ISoftDeletable entity, DateTime nowUtc)
    {
        entity.IsActive = false;
        entity.DeactivatedAtUtc = nowUtc;
    }

    // Restores exactly the pre-deactivation state — no re-validation of any field (§2: "no
    // guard on reactivate... not re-validated against BR-004 at reactivation time").
    public static void Reactivate(this ISoftDeletable entity)
    {
        entity.IsActive = true;
        entity.DeactivatedAtUtc = null;
    }
}
