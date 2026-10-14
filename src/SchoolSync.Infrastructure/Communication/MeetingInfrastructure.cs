using Azure.Communication.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Communication;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Communication;

// Tenant-filtered: Meeting is reached only through the tenant-filtered Rooms set.
public class MeetingRepository(SchoolSyncDbContext db) : IMeetingRepository
{
    public Task<bool> IsMemberOfActiveRoomAsync(Guid roomId, Guid userId, CancellationToken ct) =>
        db.RoomMemberships.AnyAsync(m => m.RoomId == roomId && m.UserId == userId && m.Room.IsActive, ct);

    public Task<bool> RoomIsActiveAsync(Guid roomId, CancellationToken ct) => db.Rooms.AnyAsync(r => r.Id == roomId && r.IsActive, ct);

    public Task<Meeting?> FindLiveMeetingAsync(Guid roomId, CancellationToken ct) =>
        db.Rooms.Where(r => r.Id == roomId).SelectMany(r => r.Meetings).FirstOrDefaultAsync(m => m.EndedAtUtc == null, ct);

    public async Task AddMeetingAsync(Meeting meeting, CancellationToken ct) => await db.Meetings.AddAsync(meeting, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public void DiscardPendingChanges()
    {
        foreach (var entry in db.ChangeTracker.Entries<Meeting>().Where(e => e.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;
    }
}

internal static class LiveMeetings
{
    // Q-220 end condition, shared by room removal and last-member leave. Loads and updates tracked
    // rows (never ExecuteUpdate — CT-003's guard test forbids it in src/); the caller saves.
    public static async Task EndAsync(SchoolSyncDbContext db, Guid roomId, DateTime endedAtUtc, CancellationToken ct)
    {
        foreach (var meeting in await db.Rooms.Where(r => r.Id == roomId)
                     .SelectMany(r => r.Meetings).Where(m => m.EndedAtUtc == null).ToListAsync(ct))
            meeting.EndedAtUtc = endedAtUtc;
    }
}

// Q-221: the real issuer, registered only when ACS_CONNECTION_STRING (CFG-SEC-04) is configured.
// A fresh ACS identity per request (Q-220 — no persisted mapping), VoIP scope, SDK-default lifetime.
public class AcsMeetingTokenIssuer(CommunicationIdentityClient client) : IMeetingTokenIssuer
{
    public async Task<IssuedAcsToken?> IssueAsync(CancellationToken ct)
    {
        var created = await client.CreateUserAndTokenAsync([CommunicationTokenScope.VoIP], cancellationToken: ct);
        return new IssuedAcsToken(created.Value.AccessToken.Token, created.Value.AccessToken.ExpiresOn.UtcDateTime);
    }
}

public class UnconfiguredMeetingTokenIssuer : IMeetingTokenIssuer
{
    public Task<IssuedAcsToken?> IssueAsync(CancellationToken ct) => Task.FromResult<IssuedAcsToken?>(null);
}
