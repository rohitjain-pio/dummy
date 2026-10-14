using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Communication;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Communication;

// Tenant-filtered by the CT-001 convention; no IgnoreQueryFilters().
public class AnnouncementRepository(SchoolSyncDbContext db) : IAnnouncementRepository
{
    public async Task AddAsync(Announcement announcement, CancellationToken ct) => await db.Announcements.AddAsync(announcement, ct);

    public Task<Announcement?> FindAsync(Guid id, CancellationToken ct) => db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Announcement>> ListNotWithdrawnAsync(CancellationToken ct) =>
        await db.Announcements.Where(a => !a.IsWithdrawn)
            .OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
