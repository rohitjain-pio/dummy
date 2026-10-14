using System.Collections.Concurrent;

namespace SchoolSync.Api.Hubs;

// EVT-002 presence: a user is "online" in a room while at least one of their connections has
// joined it (several tabs count once). Only the 0->1 and 1->0 transitions are broadcast.
//
// Known limit, recorded in the build log: the counts live in this process. With Azure SignalR in
// Default mode (Q-221) each connection is still owned by one app instance, so a user's two tabs on
// two instances each count 1 locally — presence can flicker "offline" while another tab remains.
// A shared store would fix it; the specs name none.
public class RoomPresenceTracker
{
    private readonly ConcurrentDictionary<(Guid RoomId, Guid UserId), int> _connections = new();

    // True when this connection made the user newly online in the room.
    public bool Connect(Guid roomId, Guid userId) =>
        _connections.AddOrUpdate((roomId, userId), 1, (_, count) => count + 1) == 1;

    // True when this was the user's last connection in the room.
    public bool Disconnect(Guid roomId, Guid userId)
    {
        var key = (roomId, userId);
        while (_connections.TryGetValue(key, out var count))
        {
            if (count <= 1)
            {
                if (_connections.TryRemove(new KeyValuePair<(Guid, Guid), int>(key, count))) return true;
            }
            else if (_connections.TryUpdate(key, count - 1, count))
            {
                return false;
            }
        }
        return false;
    }
}
