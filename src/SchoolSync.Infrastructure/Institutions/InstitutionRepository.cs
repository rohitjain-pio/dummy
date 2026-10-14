using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Institutions;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Institutions;

public class InstitutionRepository(SchoolSyncDbContext db) : IInstitutionRepository
{
    public Task<bool> DuplicateExistsAsync(string name, string contactEmail, string postalCode, CancellationToken ct) =>
        // BRL-003/E1: must see withdrawn institutions too — Institution has no global query
        // filter (it isn't ITenantOwned; the tenant filter is irrelevant here regardless), so
        // this already sees every institution's rows including IsActive=false ones. No
        // IgnoreQueryFilters() call is needed and none is used.
        db.Institutions.AnyAsync(i => i.Name == name && i.ContactEmail == contactEmail && i.PostalCode == postalCode, ct);

    public async Task AddAsync(Institution institution, CancellationToken ct) =>
        await db.Institutions.AddAsync(institution, ct);

    public Task<Institution?> FindByIdIgnoringTenantAsync(Guid id, CancellationToken ct) =>
        // Institution is not ITenantOwned, so there is no tenant filter to ignore here either —
        // named for symmetry with the interface's intent (any institution, not just the
        // caller's), not because a filter actually needs bypassing.
        db.Institutions.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<SubscriptionPlan?> FindEntryPlanAsync(CancellationToken ct) =>
        db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Name == "Entry", ct);

    public async Task<IReadOnlyList<PublicInstitutionDirectoryProjection>> SearchDirectoryAsync(string? query, CancellationToken ct)
    {
        var q = db.PublicInstitutionDirectory.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            // D12: SQL LIKE search, no dedicated search engine.
            var pattern = $"%{query}%";
            q = q.Where(p => EF.Functions.Like(p.Name, pattern)
                           || EF.Functions.Like(p.City, pattern)
                           || EF.Functions.Like(p.Region, pattern));
        }
        return await q.ToListAsync(ct);
    }

    public async Task UpsertDirectoryProjectionAsync(Institution institution, CancellationToken ct)
    {
        // A5: synchronous refresh on institution create/update/withdraw. Withdrawn institutions
        // are removed from the searchable directory (a withdrawn school shouldn't surface in
        // public self-registration search) but the Institution row itself is never deleted
        // (BR-005) — only its projection row is.
        var existing = await db.PublicInstitutionDirectory.FindAsync([institution.Id], ct);
        if (!institution.IsActive)
        {
            if (existing is not null) db.PublicInstitutionDirectory.Remove(existing);
            return;
        }

        if (existing is null)
        {
            db.PublicInstitutionDirectory.Add(new PublicInstitutionDirectoryProjection
            {
                InstitutionId = institution.Id,
                Name = institution.Name,
                City = institution.City,
                Region = institution.Region
            });
        }
        else
        {
            existing.Name = institution.Name;
            existing.City = institution.City;
            existing.Region = institution.Region;
        }
    }

    public Task<IdempotencyRecord?> FindIdempotencyRecordAsync(string key, CancellationToken ct) =>
        db.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == key, ct);

    public async Task AddIdempotencyRecordAsync(IdempotencyRecord record, CancellationToken ct) =>
        await db.IdempotencyRecords.AddAsync(record, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
