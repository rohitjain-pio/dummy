using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Communication;

public record SentMessage(MessageView Message, Guid? ClientMessageId, bool WasDuplicate);

// CT-303: the message and membership operations behind the collaboration hub (EVT-001, explicit
// leave) and API-046 (revise/withdraw). Transport-free, so the rules are testable without SignalR.
public interface IMessagingService
{
    Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct);
    Task<CommunicationResult<SentMessage>> SendAsync(Guid roomId, Guid? clientMessageId, string? content, CommunicationCaller caller, CancellationToken ct); // EVT-001
    Task<CommunicationResult<MessageView>> ReviseAsync(Guid roomId, Guid messageId, string? content, CommunicationCaller caller, CancellationToken ct); // API-046 PUT
    Task<CommunicationResult<MessageView>> WithdrawAsync(Guid roomId, Guid messageId, CommunicationCaller caller, CancellationToken ct); // API-046 DELETE
    Task<CommunicationResult<bool>> LeaveAsync(Guid roomId, CommunicationCaller caller, CancellationToken ct); // Q-219 explicit leave
}

public interface IMessagingRepository
{
    Task<bool> IsMemberOfActiveRoomAsync(Guid roomId, Guid userId, CancellationToken ct);
    Task<RoomMembership?> FindMembershipAsync(Guid roomId, Guid userId, CancellationToken ct);
    void RemoveMembership(RoomMembership membership);
    Task<bool> HasOtherMembersAsync(Guid roomId, Guid exceptUserId, CancellationToken ct);
    Task EndLiveMeetingsAsync(Guid roomId, DateTime endedAtUtc, CancellationToken ct); // Q-220
    Task<Message?> FindByClientMessageIdAsync(Guid roomId, Guid authorId, Guid clientMessageId, CancellationToken ct);
    Task<Message?> FindMessageAsync(Guid roomId, Guid messageId, CancellationToken ct); // via the tenant-filtered room
    Task AddMessageAsync(Message message, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

// Server -> client notifications that originate outside the hub (a REST join creates membership).
// Implemented in the Api layer over IHubContext; the Application layer stays transport-free.
public interface IRoomEventPublisher
{
    Task ParticipantJoinedAsync(Guid roomId, Guid userId, CancellationToken ct); // EVT-004 "joined"
}
