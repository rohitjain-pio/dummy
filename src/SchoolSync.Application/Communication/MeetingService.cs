using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Communication;

// API-047 (CT-304). BRL-046, PF-009, Q-220/221.
public class MeetingService(IMeetingRepository repository, IMeetingTokenIssuer issuer, TimeProvider clock) : IMeetingService
{
    public async Task<CommunicationResult<MeetingTokenResult>> IssueTokenAsync(Guid roomId, CommunicationCaller caller, CancellationToken ct)
    {
        if (!await repository.RoomIsActiveAsync(roomId, ct))
            return CommunicationResult<MeetingTokenResult>.Fail(CommunicationFailureReason.NotFound);

        // BRL-046 / TC-507: membership first — a non-member never causes an ACS identity or token
        // to be minted, and never starts a meeting.
        if (!await repository.IsMemberOfActiveRoomAsync(roomId, caller.UserId, ct))
            return CommunicationResult<MeetingTokenResult>.Fail(CommunicationFailureReason.NotRoomMember);

        var token = await issuer.IssueAsync(ct);
        if (token is null) // Q-221: no ACS in this environment — fail visibly, and start no meeting
            return CommunicationResult<MeetingTokenResult>.Fail(CommunicationFailureReason.MeetingsUnavailable);

        var meeting = await FindOrStartLiveMeetingAsync(roomId, ct);
        return CommunicationResult<MeetingTokenResult>.Ok(new MeetingTokenResult(token.Token, token.ExpiresAtUtc, meeting.Id));
    }

    private async Task<Meeting> FindOrStartLiveMeetingAsync(Guid roomId, CancellationToken ct)
    {
        if (await repository.FindLiveMeetingAsync(roomId, ct) is { } live) return live;

        var meeting = new Meeting { Id = Guid.NewGuid(), RoomId = roomId, StartedAtUtc = clock.GetUtcNow().UtcDateTime };
        await repository.AddMeetingAsync(meeting, ct);
        try
        {
            await repository.SaveChangesAsync(ct);
            return meeting;
        }
        catch (Exception)
        {
            // Two members asked at the same instant: the filtered unique index let one insert win.
            // Drop our losing insert and join the winner's meeting, so both share one group call.
            // Anything else (no winner exists) is a real failure and is rethrown, never swallowed.
            repository.DiscardPendingChanges();
            var winner = await repository.FindLiveMeetingAsync(roomId, ct);
            if (winner is null) throw;
            return winner;
        }
    }
}
