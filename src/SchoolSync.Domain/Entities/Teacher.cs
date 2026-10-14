using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// ENT-005. 04-data-model.md §2.4.
public class Teacher : ITenantOwned, ISoftDeletable, IAuditable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public required string Name { get; set; }
    public required string ContactEmail { get; set; }
    public required string Telephone { get; set; }
    public required string Gender { get; set; }
    public required string Address { get; set; }
    public required DateOnly JoiningDate { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAtUtc { get; set; }

    // §4 concurrency policy: teacher edits are an ordinary edit-in-place operation.
    public byte[] RowVersion { get; set; } = [];

    public string GetAuditEntityId() => Id.ToString();
}
