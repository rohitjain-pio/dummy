using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// ENT-015, platform-global — no InstitutionId, not ITenantOwned. 04-data-model.md §2.10.
// Exactly 3 seed rows (BR-061): Entry, Mid, Bespoke. Q-227 (Rashmi Mehta, 2026-09-14): the seeded
// capacity/storage/capability values have no source in the BRD or specs — they are provisional
// defaults, editable by a PlatformOperator, and every edit is audited.
public class SubscriptionPlan : IAuditable
{
    public string GetAuditEntityId() => Id.ToString();

    public Guid Id { get; set; }
    public required string Name { get; set; }
    public int LearnerCapacity { get; set; }
    public int StorageAllowanceGb { get; set; }
    public required string SupportLevel { get; set; }

    // JSON array, e.g. ["AdvancedAnalytics","ParentAccess","CustomReporting"] — read by the
    // tier-gate check in 07-business-logic/configuration-commercial.md.
    public required string CapabilitiesJson { get; set; }
}
