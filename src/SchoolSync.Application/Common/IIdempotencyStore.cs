namespace SchoolSync.Application.Common;

// The Idempotency-Key replay store for POST endpoints the contracts mark idempotent
// (05-api-contracts/communication.md "Idempotency": POST /rooms, POST /announcements). Same
// IdempotencyRecord table and 24-hour window CT-101 introduced for API-001; the key is namespaced by
// endpoint and institution so a client-chosen key can never replay another tenant's response.
public record StoredResponse(int StatusCode, string BodyJson);

public interface IIdempotencyStore
{
    Task<StoredResponse?> FindAsync(string scope, Guid institutionId, string key, CancellationToken ct);
    Task SaveAsync(string scope, Guid institutionId, string key, StoredResponse response, CancellationToken ct);
}
