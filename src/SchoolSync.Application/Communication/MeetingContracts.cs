using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Communication;

// API-047's success body. Q-220 adds GroupCallId (= Meeting.Id) to the contract's {acsToken,
// expiresAtUtc}: every member's browser SDK must join the same ACS group call, and the client has
// no other way to learn which one.
public record MeetingTokenResult(string AcsToken, DateTime ExpiresAtUtc, Guid GroupCallId);

public record IssuedAcsToken(string Token, DateTime ExpiresAtUtc);

// Tech Design §6: the API only issues short-lived ACS Calling tokens; it never relays media.
// Q-221: null means ACS is not configured in this environment (no ACS_CONNECTION_STRING).
public interface IMeetingTokenIssuer
{
    Task<IssuedAcsToken?> IssueAsync(CancellationToken ct);
}

public interface IMeetingService
{
    Task<CommunicationResult<MeetingTokenResult>> IssueTokenAsync(Guid roomId, CommunicationCaller caller, CancellationToken ct); // API-047
}

public interface IMeetingRepository
{
    Task<bool> IsMemberOfActiveRoomAsync(Guid roomId, Guid userId, CancellationToken ct);
    Task<bool> RoomIsActiveAsync(Guid roomId, CancellationToken ct);
    Task<Meeting?> FindLiveMeetingAsync(Guid roomId, CancellationToken ct); // via the tenant-filtered room
    Task AddMeetingAsync(Meeting meeting, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    void DiscardPendingChanges();
}
