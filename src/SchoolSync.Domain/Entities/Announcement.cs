using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// ENT-009. 04-data-model.md §2.7.
public class Announcement : ITenantOwned, IAuditable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public required string Title { get; set; } // VAL-016
    public required string Content { get; set; }
    public DateOnly EffectiveDate { get; set; } // VAL-017

    public Guid AuthorId { get; set; } // required, immutable
    public UserAccount Author { get; set; } = null!;

    // BRL-039 names `CreatedAtUtc` as immutable and BR-039 retains "author + date"; 04-data-model.md
    // §2.7 omits the column. Added in CT-301 on BRL-039's authority (recorded as a silence).
    public DateTime CreatedAtUtc { get; set; }

    public bool IsWithdrawn { get; set; }
    public DateTime? WithdrawnAtUtc { get; set; }

    public string GetAuditEntityId() => Id.ToString();
}
