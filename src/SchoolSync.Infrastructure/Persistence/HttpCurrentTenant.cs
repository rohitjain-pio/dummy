using Microsoft.AspNetCore.Http;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Infrastructure.Persistence;

// The real, request-scoped ICurrentTenant (CT-001 deliberately shipped with none registered).
// Resolves institution_id from the signed-in user's JWT claim (09-security-spec.md §1).
public class HttpCurrentTenant(IHttpContextAccessor httpContextAccessor) : ICurrentTenant
{
    public Guid? InstitutionId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;
            // ADR-0007: outside any request (a background job), the job's explicitly entered scope.
            if (httpContext is null) return BackgroundTenantScope.InstitutionId;

            var claim = httpContext.User.FindFirst("institution_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    // BR-013: true only for an authenticated PlatformOperator. This is the one named,
    // explicitly-marked cross-tenant path — never a general escape hatch, and never settable by
    // any other role's claims.
    public bool BypassTenantFilter =>
        httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
            == nameof(UserRole.PlatformOperator);
}
