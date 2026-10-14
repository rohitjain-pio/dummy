using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// ENT-010. 04-data-model.md §2.8.
// IsActive/DeactivatedAtUtc added per CT-004 (silence report): 04-data-model.md §2.8 never lists
// these columns for Room, but 07-business-logic/_shared.md §2 (BR-005/D8) explicitly names Room
// as one of the five soft-deletable entities. _shared.md is the authoritative cross-cutting
// policy for this exact pattern; treated as resolving the gap, not as a new decision.
public class Room : ITenantOwned, ISoftDeletable
{
    public Guid Id { get; set; }
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    public required string Name { get; set; }
    public string? Description { get; set; }

    // Never selected into any list/detail DTO. FR-043/Q-M4: never displayed after creation.
    public required string PasscodeHash { get; set; }

    public Guid CreatedByUserId { get; set; }
    public UserAccount CreatedBy { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public DateTime? DeactivatedAtUtc { get; set; }

    public ICollection<Message> Messages { get; set; } = [];

    // Q-220: many meetings over a room's life, at most one live (EndedAtUtc null) at a time —
    // enforced by a filtered unique index. The initial schema mapped this one-to-one.
    public ICollection<Meeting> Meetings { get; set; } = [];
}

// ENT-011. 04-data-model.md §2.8.
public class Message : IAuditable
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public Guid AuthorId { get; set; }
    public UserAccount Author { get; set; } = null!;

    public required string Content { get; set; } // <= 4000 chars, VAL-020
    public DateTime SentAtUtc { get; set; }

    // BRL-044: content blanked at read time when withdrawn; row retained, never deleted.
    public bool IsWithdrawn { get; set; }

    // CT-303. BRL-044 names a WithdrawnAtUtc flag; §2.8 lists only is_withdrawn.
    public DateTime? WithdrawnAtUtc { get; set; }

    // CT-303. Set when the author revises the content (API-046), so a reader can tell the text
    // changed after it was sent (NFR-030 attribution). The prior text is in the audit before-image.
    public DateTime? RevisedAtUtc { get; set; }

    // CT-303. 06-event-contracts.md EVT-001: a client-generated id; a repeated id from the same
    // author in the same room is a no-op returning the original message. Unique per (room, author).
    public Guid? ClientMessageId { get; set; }

    public string GetAuditEntityId() => Id.ToString();
}

// ENT-012. 04-data-model.md §2.8.
public class Meeting
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; } // nullable while live
}
