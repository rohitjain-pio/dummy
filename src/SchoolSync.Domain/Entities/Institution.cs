using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// ENT-001. 04-data-model.md §2.1.
// IAuditable since CT-503: NFR-032 names subscription changes as audited, and the plan lives here.
public class Institution : ISoftDeletable, IAuditable
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string RegistrationNumber { get; set; }
    public required string ContactEmail { get; set; } // VAL-001
    public required string Telephone { get; set; } // VAL-003
    public required string PostalAddress { get; set; }
    public required string PostalCode { get; set; } // VAL-004
    public required string City { get; set; } // feeds PublicInstitutionDirectoryProjection
    public required string Region { get; set; } // feeds PublicInstitutionDirectoryProjection

    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAtUtc { get; set; }

    // BRL-074 / Q-211: never null — auto-set to the Entry/Free tier at creation, in the same
    // transaction as the institution row itself. No post-creation window exists where this is null.
    public Guid SubscriptionPlanId { get; set; }
    public SubscriptionPlan SubscriptionPlan { get; set; } = null!;

    // API-064's startDate (FR-072 "assigns/amends subscription plan + date"); §2.1 has no column for it.
    public DateOnly? SubscriptionStartDate { get; set; }

    // Q-230 (CT-504): a running trial grants TrialPlanId until TrialExpiresAtUtc. TrialStartedAtUtc is
    // never cleared, so a trial can be started only once per institution.
    public Guid? TrialPlanId { get; set; }
    public DateTime? TrialStartedAtUtc { get; set; }
    public DateTime? TrialExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // §4 concurrency policy: institution profile is an ordinary edit-in-place operation.
    public byte[] RowVersion { get; set; } = [];

    public string GetAuditEntityId() => Id.ToString();
}
