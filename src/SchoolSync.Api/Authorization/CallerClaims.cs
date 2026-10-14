using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Api.Authorization;

// The three claims every tenant-scoped caller carries (09-security-spec.md §1: sub, role,
// institution_id), read one way. Used by the P3 controllers and the collaboration hub.
public static class CallerClaims
{
    public static Guid UserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    public static UserRole Role(this ClaimsPrincipal user) => Enum.Parse<UserRole>(user.FindFirstValue(ClaimTypes.Role)!);
    public static Guid InstitutionId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirst("institution_id")!.Value);
}
