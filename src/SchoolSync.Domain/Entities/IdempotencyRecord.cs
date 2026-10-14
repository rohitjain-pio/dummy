namespace SchoolSync.Domain.Entities;

// API-001's Idempotency-Key header: a repeated key within 24h returns the original 201 body
// rather than creating a second row — distinct from BRL-003's content-based duplicate check.
// Platform-global (no institution scoping needed — a key is caller-supplied and globally unique
// by construction, a GUID in practice).
public class IdempotencyRecord
{
    public required string Key { get; set; } // PK
    public required string ResponseBodyJson { get; set; }
    public int ResponseStatusCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public bool IsExpired(DateTime nowUtc) => nowUtc - CreatedAtUtc > TimeSpan.FromHours(24);
}
