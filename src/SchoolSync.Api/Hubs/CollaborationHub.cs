using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SchoolSync.Api.Authorization;
using SchoolSync.Application.Communication;

namespace SchoolSync.Api.Hubs;

// `/hubs/collaboration` (T-001, CT-303). Group = room id. 06-event-contracts.md §1. Client method
// names are the event names from 02-requirements-spec/communication.md.
//
// Authorization, in two layers:
//  - membership (Q-219) lives in the database and is re-checked on EVERY JoinRoom/SendMessage/
//    Typing/SetMediaState call, so a removed room or an explicit leave takes effect at once;
//  - a connection must also have called JoinRoom for the room, so an event is never broadcast to a
//    group the sender is not in.
[Authorize]
public class CollaborationHub(IMessagingService messaging, RoomPresenceTracker presence) : Hub
{
    public static class Events
    {
        public const string MessageSent = "MessageSent"; // EVT-001
        public const string PresenceChanged = "PresenceChanged"; // EVT-002
        public const string TypingChanged = "TypingChanged"; // EVT-003
        public const string ParticipantJoined = "ParticipantJoined"; // EVT-004 "joined"
        public const string ParticipantLeft = "ParticipantLeft"; // EVT-004 "left"
        public const string MediaStateChanged = "MediaStateChanged"; // EVT-005
        public const string HistoryResync = "HistoryResync"; // EVT-006
    }

    public record MessageAck(Guid MessageId, Guid? ClientMessageId, DateTime SentAtUtc, bool WasDuplicate);

    private const string JoinedRoomsKey = "joined-rooms";

    private CommunicationCaller Caller => new(Context.User!.UserId(), Context.User!.Role(), Context.User!.InstitutionId());

    private HashSet<Guid> JoinedRooms =>
        (HashSet<Guid>)(Context.Items.TryGetValue(JoinedRoomsKey, out var rooms) ? rooms! : Context.Items[JoinedRoomsKey] = new HashSet<Guid>());

    private static HubException Refused(string code) => new(code); // the client maps the ERR- code to catalogue wording

    private async Task RequireJoinedMemberAsync(Guid roomId)
    {
        if (!JoinedRooms.Contains(roomId) || !await messaging.IsMemberAsync(roomId, Caller.UserId, Context.ConnectionAborted))
            throw Refused("ERR-102");
    }

    public async Task JoinRoom(Guid roomId)
    {
        if (!await messaging.IsMemberAsync(roomId, Caller.UserId, Context.ConnectionAborted))
            throw Refused("ERR-102"); // API-043 (correct passcode) must come first

        if (JoinedRooms.Add(roomId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId.ToString());
            if (presence.Connect(roomId, Caller.UserId))
                await Clients.OthersInGroup(roomId.ToString()).SendAsync(Events.PresenceChanged, new { roomId, userId = Caller.UserId, status = "online" });
        }

        // EVT-006: on every (re)join, tell this client to re-fetch history over REST (API-044).
        // The hub never replays a backlog (NFR-012); `messages` is empty by design.
        await Clients.Caller.SendAsync(Events.HistoryResync, new { roomId, messages = Array.Empty<object>() });
    }

    public async Task<MessageAck> SendMessage(Guid roomId, Guid clientMessageId, string content)
    {
        await RequireJoinedMemberAsync(roomId);

        var result = await messaging.SendAsync(roomId, clientMessageId, content, Caller, Context.ConnectionAborted);
        if (!result.Succeeded)
            throw Refused(result.Failure switch
            {
                CommunicationFailureReason.MessageTooLong => "ERR-504",
                CommunicationFailureReason.Forbidden => "ERR-102",
                _ => "ERR-901"
            });

        var sent = result.Value!;
        if (!sent.WasDuplicate) // EVT-001 idempotency: a retried send is acknowledged, never re-broadcast
        {
            await Clients.Group(roomId.ToString()).SendAsync(Events.MessageSent, new
            {
                roomId,
                messageId = sent.Message.Id,
                clientMessageId = sent.ClientMessageId,
                authorId = sent.Message.AuthorId,
                content = sent.Message.Content,
                sentAtUtc = sent.Message.SentAtUtc
            });
        }
        return new MessageAck(sent.Message.Id, sent.ClientMessageId, sent.Message.SentAtUtc, sent.WasDuplicate);
    }

    public async Task Typing(Guid roomId, bool isTyping)
    {
        await RequireJoinedMemberAsync(roomId);
        await Clients.OthersInGroup(roomId.ToString()).SendAsync(Events.TypingChanged, new { roomId, userId = Caller.UserId, isTyping });
    }

    public async Task SetMediaState(Guid roomId, bool mic, bool camera)
    {
        await RequireJoinedMemberAsync(roomId);
        await Clients.OthersInGroup(roomId.ToString()).SendAsync(Events.MediaStateChanged, new { roomId, userId = Caller.UserId, mic, camera });
    }

    // Q-219: an explicit leave ends membership; the passcode is needed to come back.
    public async Task LeaveRoom(Guid roomId)
    {
        var left = await messaging.LeaveAsync(roomId, Caller, Context.ConnectionAborted);
        if (JoinedRooms.Remove(roomId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId.ToString());
            if (presence.Disconnect(roomId, Caller.UserId))
                await Clients.Group(roomId.ToString()).SendAsync(Events.PresenceChanged, new { roomId, userId = Caller.UserId, status = "offline" });
        }
        if (left.Value)
            await Clients.Group(roomId.ToString()).SendAsync(Events.ParticipantLeft, new { roomId, userId = Caller.UserId, @event = "left" });
    }

    // Q-219: a dropped connection is presence only (EVT-002 "offline"), never a leave.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var roomId in JoinedRooms)
        {
            if (presence.Disconnect(roomId, Caller.UserId))
                await Clients.Group(roomId.ToString()).SendAsync(Events.PresenceChanged, new { roomId, userId = Caller.UserId, status = "offline" });
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public class HubRoomEventPublisher(IHubContext<CollaborationHub> hub) : IRoomEventPublisher
{
    public Task ParticipantJoinedAsync(Guid roomId, Guid userId, CancellationToken ct) =>
        hub.Clients.Group(roomId.ToString()).SendAsync(CollaborationHub.Events.ParticipantJoined, new { roomId, userId, @event = "joined" }, ct);
}
