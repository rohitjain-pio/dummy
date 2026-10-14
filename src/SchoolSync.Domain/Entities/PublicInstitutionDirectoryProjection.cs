namespace SchoolSync.Domain.Entities;

// INFERRED, not given a column list in 04-data-model.md §2 (silence report, CT-001) — derived
// from Q-H7's resolution ("public institution directory exposes institution name and city/region
// only, for disambiguation purposes"), D12 (SQL LIKE/full-text search, no dedicated search
// engine), and Tech Design A5 (refreshed synchronously on institution create/update/withdraw).
// Platform-global (no InstitutionId FK filter applied — it is not ITenantOwned, since the whole
// point is that it's readable pre-authentication by design), but does carry the source
// InstitutionId so a refresh can target/replace the right row.
public class PublicInstitutionDirectoryProjection
{
    public Guid InstitutionId { get; set; } // PK, mirrors the source Institution.Id
    public required string Name { get; set; }
    public required string City { get; set; }
    public required string Region { get; set; }
}
