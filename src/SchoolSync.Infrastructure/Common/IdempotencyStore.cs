using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Common;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Common;

public class IdempotencyStore(SchoolSyncDbContext db, TimeProvider clock) : IIdempotencyStore
{
    private static string StorageKey(string scope, Guid institutionId, string key) => $"{scope}:{institutionId:N}:{key}";

    public async Task<StoredResponse?> FindAsync(string scope, Guid institutionId, string key, CancellationToken ct)
    {
        var record = await db.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == StorageKey(scope, institutionId, key), ct);
        return record is null || record.IsExpired(clock.GetUtcNow().UtcDateTime)
            ? null
            : new StoredResponse(record.ResponseStatusCode, record.ResponseBodyJson);
    }

    public async Task SaveAsync(string scope, Guid institutionId, string key, StoredResponse response, CancellationToken ct)
    {
        var storageKey = StorageKey(scope, institutionId, key);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var existing = await db.IdempotencyRecords.FirstOrDefaultAsync(r => r.Key == storageKey, ct);
        if (existing is null)
        {
            db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = storageKey,
                ResponseBodyJson = response.BodyJson,
                ResponseStatusCode = response.StatusCode,
                CreatedAtUtc = nowUtc
            });
        }
        else
        {
            // Only reachable once the old record has expired (FindAsync would have replayed it
            // otherwise): reuse the row rather than colliding on the primary key.
            existing.ResponseBodyJson = response.BodyJson;
            existing.ResponseStatusCode = response.StatusCode;
            existing.CreatedAtUtc = nowUtc;
        }
        await db.SaveChangesAsync(ct);
    }
}
