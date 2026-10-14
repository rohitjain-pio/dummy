namespace SchoolSync.Infrastructure.Persistence;

// Resolved from the signed-in user's institution_id JWT claim (D4). Registered per-request in
// the Api layer (CT-002); tests supply a fake implementation to set the tenant explicitly.
public interface ICurrentTenant
{
    // Null for PlatformOperator acting outside any single institution's scope.
    Guid? InstitutionId { get; }

    // BR-013: the only role permitted to bypass the tenant filter, and only via this explicitly
    // named path — never a silent global query. Set true only inside that documented path.
    bool BypassTenantFilter { get; }
}
