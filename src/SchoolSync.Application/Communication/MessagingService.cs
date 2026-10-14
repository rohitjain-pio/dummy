using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Communication;

// BRL-043–045, VAL-020, EVT-001 idempotency, API-046, and the Q-219 explicit leave.
public class MessagingService(IMessagingRepository repository, TimeProvider clock) : IMessagingService
{
    // CFG-003 (D3, BR-045) — a fixed platform constant, bound here as a named constant in the
    // same way CFG-004/005 are.
    public const int MaxMessageLength = 4000;

    private static CommunicationFailureReason? ContentProblem(string? content) =>
        string.IsNullOrWhiteSpace(content) ? CommunicationFailureReason.ValidationFailed // VAL-020: at least 1 character
        : content.Length > MaxMessageLength ? CommunicationFailureReason.MessageTooLong // BRL-045 -> ERR-504
        : null;

    public Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct) =>
        repository.IsMemberOfActiveRoomAsync(roomId, userId, ct);

    public async Task<CommunicationResult<SentMessage>> SendAsync(
        Guid roomId, Guid? clientMessageId, string? content, CommunicationCaller caller, CancellationToken ct)
    {
        // 06-event-contracts.md "Authorization": re-validated against the current database on every
        // send, so a removed room or an explicit leave takes effect immediately, not at reconnect.
        if (!await repository.IsMemberOfActiveRoomAsync(roomId, caller.UserId, ct))
            return CommunicationResult<SentMessage>.Fail(CommunicationFailureReason.Forbidden);
        if (ContentProblem(content) is { } problem) return CommunicationResult<SentMessage>.Fail(problem);

        if (clientMessageId is { } clientId
            && await repository.FindByClientMessageIdAsync(roomId, caller.UserId, clientId, ct) is { } original)
            return CommunicationResult<SentMessage>.Ok(new SentMessage(RoomService.ToView(original), clientId, WasDuplicate: true));

        var message = new Message
        {
            Id = Guid.NewGuid(),
            RoomId = roomId,
            AuthorId = caller.UserId, // BRL-043: attributable — always from the token
            Content = content!,
            SentAtUtc = clock.GetUtcNow().UtcDateTime,
            ClientMessageId = clientMessageId
        };
        await repository.AddMessageAsync(message, ct);
        await repository.SaveChangesAsync(ct);
        return CommunicationResult<SentMessage>.Ok(new SentMessage(RoomService.ToView(message), clientMessageId, WasDuplicate: false));
    }

    private async Task<(Message? Message, CommunicationFailureReason? Failure)> FindOwnMessageAsync(
        Guid roomId, Guid messageId, CommunicationCaller caller, CancellationToken ct)
    {
        var message = await repository.FindMessageAsync(roomId, messageId, ct);
        if (message is null) return (null, CommunicationFailureReason.NotFound);
        // BRL-044: the author only — not the room's creator, not leadership.
        return message.AuthorId == caller.UserId ? (message, null) : (null, CommunicationFailureReason.Forbidden);
    }

    public async Task<CommunicationResult<MessageView>> ReviseAsync(
        Guid roomId, Guid messageId, string? content, CommunicationCaller caller, CancellationToken ct)
    {
        var (message, failure) = await FindOwnMessageAsync(roomId, messageId, caller, ct);
        if (failure is { } f) return CommunicationResult<MessageView>.Fail(f);
        if (message!.IsWithdrawn) return CommunicationResult<MessageView>.Fail(CommunicationFailureReason.ValidationFailed);
        if (ContentProblem(content) is { } problem) return CommunicationResult<MessageView>.Fail(problem);

        message.Content = content!;
        message.RevisedAtUtc = clock.GetUtcNow().UtcDateTime;
        await repository.SaveChangesAsync(ct);
        return CommunicationResult<MessageView>.Ok(RoomService.ToView(message));
    }

    public async Task<CommunicationResult<MessageView>> WithdrawAsync(
        Guid roomId, Guid messageId, CommunicationCaller caller, CancellationToken ct)
    {
        var (message, failure) = await FindOwnMessageAsync(roomId, messageId, caller, ct);
        if (failure is { } f) return CommunicationResult<MessageView>.Fail(f);

        // BRL-044 / TC-510: a flag, never a delete. The row and its author link stay; the stored
        // content stays too (the audit trail needs it) and is blanked only when read.
        if (!message!.IsWithdrawn)
        {
            message.IsWithdrawn = true;
            message.WithdrawnAtUtc = clock.GetUtcNow().UtcDateTime;
            await repository.SaveChangesAsync(ct);
        }
        return CommunicationResult<MessageView>.Ok(RoomService.ToView(message));
    }

    public async Task<CommunicationResult<bool>> LeaveAsync(Guid roomId, CommunicationCaller caller, CancellationToken ct)
    {
        var membership = await repository.FindMembershipAsync(roomId, caller.UserId, ct);
        if (membership is null) return CommunicationResult<bool>.Ok(false);

        repository.RemoveMembership(membership);
        // Q-220: the meeting ends when the room's last member leaves.
        if (!await repository.HasOtherMembersAsync(roomId, caller.UserId, ct))
            await repository.EndLiveMeetingsAsync(roomId, clock.GetUtcNow().UtcDateTime, ct);
        await repository.SaveChangesAsync(ct);
        return CommunicationResult<bool>.Ok(true);
    }
}
