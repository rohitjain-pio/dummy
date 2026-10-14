namespace SchoolSync.Domain.Common;

// D8: uniform soft-delete pattern. No hard-delete path exists for entities implementing this.
public interface ISoftDeletable
{
    bool IsActive { get; set; }
    DateTime? DeactivatedAtUtc { get; set; }
}
