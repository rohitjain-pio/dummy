using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// BR-007: closed set of 7 roles.
public enum UserRole
{
    PlatformOperator,
    Administrator,
    Principal,
    SchoolInCharge,
    Teacher,
    Student,
    Parent
}

// ENT-002. 04-data-model.md §2.2.
public class UserAccount : ITenantOwned, ISoftDeletable
{
    public Guid Id { get; set; }

    // BRL-008: required, immutable after insert.
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public required string Username { get; set; } // unique, platform-global, VAL-006
    public required string Email { get; set; } // unique, platform-global
    public required string PasswordHash { get; set; } // never selected in any list query
    public UserRole Role { get; set; }

    public Guid? StudentId { get; set; } // FR-016
    public Student? Student { get; set; }
    public Guid? TeacherId { get; set; } // FR-023

    /// <summary>
    /// CT-801 / U43, 04-data-model.md §2.2. Set for every account created by redeeming a
    /// LeadershipCode; NULL means the account predates 1.0.5 and its provenance is NOT attributable.
    /// That is the point: `WHERE role NOT IN (Student, Parent) AND provisioned_by_code_id IS NULL`
    /// returns exactly the accounts a human must review, and it stays answerable after the
    /// CMP-001/002/003 retention sweep prunes the AuditLog rows that would otherwise be the only
    /// record. **An addition beyond defect-fixing (Q-241) — strikeable by the operator.**
    /// </summary>
    public Guid? ProvisionedByCodeId { get; set; }
    public Teacher? Teacher { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAtUtc { get; set; }
}
