namespace SchoolSync.Domain.Common;

// T-002/D4: every tenant-owned table carries institution_id. The Infrastructure layer applies
// a global query filter to every type implementing this interface (04-data-model.md §3).
public interface ITenantOwned
{
    Guid InstitutionId { get; set; }
}
