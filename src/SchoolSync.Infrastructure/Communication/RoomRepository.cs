using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Communication;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Communication;

// Tenant-filtered by the CT-001 convention; no IgnoreQueryFilters().
public class RoomRepository(SchoolSyncDbContext db) : IRoomRepository
{
    public async Task AddRoomAsync(Room room, CancellationToken ct) => await db.Rooms.AddAsync(room, ct);

    public Task<Room?> FindActiveRoomAsync(Guid roomId, CancellationToken ct) =>
        db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId && r.IsActive, ct);

    public async Task<IReadOnlyList<Room>> ListActiveRoomsAsync(CancellationToken ct) =>
        await db.Rooms.Where(r => r.IsActive).OrderBy(r => r.Name).ToListAsync(ct);

    public Task<bool> IsMemberAsync(Guid roomId, Guid userId, CancellationToken ct) =>
        db.RoomMemberships.AnyAsync(m => m.RoomId == roomId && m.UserId == userId, ct);

    public async Task AddMembershipAsync(RoomMembership membership, CancellationToken ct) =>
        await db.RoomMemberships.AddAsync(membership, ct);

    public async Task<IReadOnlyList<RoomMembership>> ListMembershipsAsync(Guid roomId, CancellationToken ct) =>
        await db.RoomMemberships.Where(m => m.RoomId == roomId).ToListAsync(ct);

    public void RemoveMemberships(IEnumerable<RoomMembership> memberships) => db.RoomMemberships.RemoveRange(memberships);

    public Task EndLiveMeetingsAsync(Guid roomId, DateTime endedAtUtc, CancellationToken ct) => LiveMeetings.EndAsync(db, roomId, endedAtUtc, ct);

    // Message is not ITenantOwned; the join through the tenant-filtered Rooms set keeps it scoped.
    public async Task<IReadOnlyList<Message>> ListMessagesAsync(Guid roomId, CancellationToken ct) =>
        await db.Rooms.Where(r => r.Id == roomId).SelectMany(r => r.Messages)
            .OrderBy(m => m.SentAtUtc).ThenBy(m => m.Id).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
