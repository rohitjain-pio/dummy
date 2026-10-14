using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Communication;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Communication;

// Tenant-filtered by the CT-001 convention; Message is always reached through its tenant-filtered room.
public class MessagingRepository(SchoolSyncDbContext db) : IMessagingRepository
{
    public Task<bool> IsMemberOfActiveRoomAsync(Guid roomId, Guid userId, CancellationToken ct) =>
        db.RoomMemberships.AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.Room.IsActive, ct);

    public Task<RoomMembership?> FindMembershipAsync(Guid roomId, Guid userId, CancellationToken ct) =>
        db.RoomMemberships.FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId, ct);

    public void RemoveMembership(RoomMembership membership) => db.RoomMemberships.Remove(membership);

    public Task<bool> HasOtherMembersAsync(Guid roomId, Guid exceptUserId, CancellationToken ct) =>
        db.RoomMemberships.AnyAsync(m => m.RoomId == roomId && m.UserId != exceptUserId, ct);

    public Task EndLiveMeetingsAsync(Guid roomId, DateTime endedAtUtc, CancellationToken ct) => LiveMeetings.EndAsync(db, roomId, endedAtUtc, ct);

    public Task<Message?> FindByClientMessageIdAsync(Guid roomId, Guid authorId, Guid clientMessageId, CancellationToken ct) =>
        db.Rooms.Where(r => r.Id == roomId).SelectMany(r => r.Messages)
            .FirstOrDefaultAsync(m => m.AuthorId == authorId && m.ClientMessageId == clientMessageId, ct);

    public Task<Message?> FindMessageAsync(Guid roomId, Guid messageId, CancellationToken ct) =>
        db.Rooms.Where(r => r.Id == roomId && r.IsActive).SelectMany(r => r.Messages).FirstOrDefaultAsync(m => m.Id == messageId, ct);

    public async Task AddMessageAsync(Message message, CancellationToken ct) => await db.Messages.AddAsync(message, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
