using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// ENT-003 / ENT-004 (guardian fields embedded, not a separate table). 04-data-model.md §2.3.
public class Student : ITenantOwned, ISoftDeletable, IAuditable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public required string SrNumber { get; set; } // unique within institution, BR-018
    public required string RollNumber { get; set; } // unique within institution, BR-018
    public required string Name { get; set; }
    public required DateOnly DateOfBirth { get; set; } // VAL-010
    public required string Gender { get; set; }
    public required string Email { get; set; }

    // Required once enrolled; BR-019 gates attendance on this, not enrolment itself.
    public Guid? ClassId { get; set; }
    public Class? Class { get; set; }

    // BR-021: sensitive, same access rule as the row itself — not a separate ACL'd table.
    public string? GuardianFatherName { get; set; }
    public string? GuardianMotherName { get; set; }
    public string? GuardianContactEmail { get; set; }
    public string? GuardianPhone { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAtUtc { get; set; }

    // §4 concurrency policy: student edits are an ordinary edit-in-place operation.
    public byte[] RowVersion { get; set; } = [];

    public string GetAuditEntityId() => Id.ToString();
}
