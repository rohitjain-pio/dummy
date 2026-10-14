namespace SchoolSync.Domain.Entities;

// T-003. 04-data-model.md §2.12. code is unique while status='Issued' — enforced via a filtered
// unique index in SchoolSyncDbContext (status is derived from the three nullable timestamp
// columns below: unconsumed+unrevoked+unexpired = Issued).
public class ParentLinkCode
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public required string Code { get; set; } // 8 chars, A3

    public Guid IssuedByUserId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; } // A3: 7-day validity

    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
}

// 04-data-model.md §2.12. Composite PK (many-to-many) — BR-066, "linked children" plural.
public class ParentStudentLink
{
    public Guid ParentUserId { get; set; }
    public UserAccount ParentUser { get; set; } = null!;

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}
