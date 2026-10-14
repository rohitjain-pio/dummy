using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Communication;

// There is deliberately no passcode or passcode-hash member on any result type in this file
// (FR-043, VAL-019, TC-501): a response cannot leak what its shape does not carry.
public record RoomResult(Guid Id, string Name, string? Description, Guid CreatedByUserId);

// NewlyJoined drives EVT-004 "joined": membership is what joining means under Q-219, so the event
// fires when membership is created, not on every hub (re)connection.
public record RoomJoinResult(Guid RoomId, string HubPath, bool NewlyJoined);

// BRL-044 / E3: a withdrawn message keeps its place and author; its content is blanked on read.
public record MessageView(Guid Id, Guid RoomId, Guid AuthorId, string Content, DateTime SentAtUtc, bool IsWithdrawn, DateTime? RevisedAtUtc);

public interface IRoomService
{
    Task<CommunicationResult<RoomResult>> CreateAsync(string? name, string? description, string? passcode, CommunicationCaller caller, CancellationToken ct); // API-041
    Task<IReadOnlyList<RoomResult>> ListAsync(CancellationToken ct); // API-042
    Task<CommunicationResult<RoomJoinResult>> JoinAsync(Guid roomId, string? passcode, CommunicationCaller caller, CancellationToken ct); // API-043
    Task<CommunicationResult<IReadOnlyList<MessageView>>> HistoryAsync(Guid roomId, CommunicationCaller caller, CancellationToken ct); // API-044
    Task<CommunicationResult<bool>> RemoveAsync(Guid roomId, CommunicationCaller caller, CancellationToken ct); // API-045
}

// Tenant-filtered throughout: Room and RoomMembership are ITenantOwned, and Message is reached only
// through its room.
public interface IRoomRepository
{
    Task AddRoomAsync(Room room, CancellationToken ct);
    Task<Room?> FindActiveRoomAsync(Guid roomId, CancellationToken ct);
    Task<IReadOnlyList<Room>> ListActiveRoomsAsync(CancellationToken ct);

    Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct);
    Task AddMembershipAsync(RoomMembership membership, CancellationToken ct);
    Task<IReadOnlyList<RoomMembership>> ListMembershipsAsync(Guid roomId, CancellationToken ct);
    void RemoveMemberships(IEnumerable<RoomMembership> memberships);
    Task EndLiveMeetingsAsync(Guid roomId, DateTime endedAtUtc, CancellationToken ct); // Q-220

    Task<IReadOnlyList<Message>> ListMessagesAsync(Guid roomId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
