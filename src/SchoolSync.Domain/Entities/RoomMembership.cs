using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

// Q-219 (Rashmi Mehta, 2026-09-14). 04-data-model.md §2.8 has no membership entity, yet API-044
// (history) and API-047 (meeting token) are "room member" only and 06-event-contracts.md requires
// membership be "re-checked against the current DB state". A row exists from a correct-passcode
// join (API-043) until an explicit leave or the room's removal. A dropped connection does NOT remove
// it: presence goes offline, and SignalR's auto-reconnect (NFR-012) resumes without the passcode.
public class RoomMembership : ITenantOwned
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public Guid UserId { get; set; }

    // Denormalised from the room so the CT-001 tenant filter applies to membership lookups directly.
    public Guid InstitutionId { get; set; }

    public DateTime JoinedAtUtc { get; set; }
}
