using SchoolSync.Application.Auth;
using SchoolSync.Domain.Common;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Communication;

// API-041–045 (CT-302). BRL-040–042, VAL-018/019, and the Q-219 membership rules.
public class RoomService(IRoomRepository repository, IPasswordHasher hasher, TimeProvider clock) : IRoomService
{
    public const string HubPath = "/hubs/collaboration";

    private static RoomResult ToResult(Room r) => new(r.Id, r.Name, r.Description, r.CreatedByUserId);

    internal static MessageView ToView(Message m) =>
        new(m.Id, m.RoomId, m.AuthorId, m.IsWithdrawn ? string.Empty : m.Content, m.SentAtUtc, m.IsWithdrawn, m.RevisedAtUtc);

    public async Task<CommunicationResult<RoomResult>> CreateAsync(
        string? name, string? description, string? passcode, CommunicationCaller caller, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(passcode)) // VAL-018, VAL-019
            return CommunicationResult<RoomResult>.Fail(CommunicationFailureReason.ValidationFailed);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var room = new Room
        {
            Id = Guid.NewGuid(),
            InstitutionId = caller.InstitutionId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            PasscodeHash = hasher.Hash(passcode), // BCrypt, the same hasher as account passwords
            CreatedByUserId = caller.UserId
        };
        await repository.AddRoomAsync(room, ct);

        // The creator chose the passcode, so asking them to "join" their own room would verify a
        // secret they just set. They are its first member.
        await repository.AddMembershipAsync(new RoomMembership
        {
            RoomId = room.Id,
            UserId = caller.UserId,
            InstitutionId = caller.InstitutionId,
            JoinedAtUtc = nowUtc
        }, ct);
        await repository.SaveChangesAsync(ct);
        return CommunicationResult<RoomResult>.Ok(ToResult(room));
    }

    public async Task<IReadOnlyList<RoomResult>> ListAsync(CancellationToken ct) =>
        (await repository.ListActiveRoomsAsync(ct)).Select(ToResult).ToList();

    public async Task<CommunicationResult<RoomJoinResult>> JoinAsync(
        Guid roomId, string? passcode, CommunicationCaller caller, CancellationToken ct)
    {
        var room = await repository.FindActiveRoomAsync(roomId, ct);
        if (room is null) return CommunicationResult<RoomJoinResult>.Fail(CommunicationFailureReason.NotFound);

        // BRL-041: verified once, here. The passcode is checked even for an existing member, so this
        // endpoint cannot be used to confirm membership without knowing the secret.
        if (string.IsNullOrEmpty(passcode) || !hasher.Verify(passcode, room.PasscodeHash))
            return CommunicationResult<RoomJoinResult>.Fail(CommunicationFailureReason.WrongPasscode);

        var newlyJoined = !await repository.IsMemberAsync(roomId, caller.UserId, ct);
        if (newlyJoined)
        {
            await repository.AddMembershipAsync(new RoomMembership
            {
                RoomId = roomId,
                UserId = caller.UserId,
                InstitutionId = room.InstitutionId,
                JoinedAtUtc = clock.GetUtcNow().UtcDateTime
            }, ct);
            await repository.SaveChangesAsync(ct);
        }
        return CommunicationResult<RoomJoinResult>.Ok(new RoomJoinResult(roomId, HubPath, newlyJoined));
    }

    public async Task<CommunicationResult<IReadOnlyList<MessageView>>> HistoryAsync(Guid roomId, CommunicationCaller caller, CancellationToken ct)
    {
        if (await repository.FindActiveRoomAsync(roomId, ct) is null)
            return CommunicationResult<IReadOnlyList<MessageView>>.Fail(CommunicationFailureReason.NotFound);
        if (!await repository.IsMemberAsync(roomId, caller.UserId, ct)) // API-044: room member only
            return CommunicationResult<IReadOnlyList<MessageView>>.Fail(CommunicationFailureReason.Forbidden);

        IReadOnlyList<MessageView> history = (await repository.ListMessagesAsync(roomId, ct)).Select(ToView).ToList();
        return CommunicationResult<IReadOnlyList<MessageView>>.Ok(history);
    }

    public async Task<CommunicationResult<bool>> RemoveAsync(Guid roomId, CommunicationCaller caller, CancellationToken ct)
    {
        var room = await repository.FindActiveRoomAsync(roomId, ct);
        if (room is null) return CommunicationResult<bool>.Fail(CommunicationFailureReason.NotFound);

        // BRL-042 / E2: creator OR leadership — leadership's clause is what keeps a room removable
        // after its creator's account is deactivated.
        if (room.CreatedByUserId != caller.UserId && !caller.IsLeadership)
            return CommunicationResult<bool>.Fail(CommunicationFailureReason.RoomRemovalForbidden);

        // D8: Room is soft-deletable, so removal deactivates it and message history stays (BR-043).
        // Memberships go, so no one can reach the removed room through the hub or the meeting token.
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        room.Deactivate(nowUtc);
        repository.RemoveMemberships(await repository.ListMembershipsAsync(roomId, ct));
        await repository.EndLiveMeetingsAsync(roomId, nowUtc, ct); // Q-220
        await repository.SaveChangesAsync(ct);
        return CommunicationResult<bool>.Ok(true);
    }
}
