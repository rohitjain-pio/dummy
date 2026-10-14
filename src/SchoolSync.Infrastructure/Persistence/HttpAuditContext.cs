using Microsoft.AspNetCore.Http;

namespace SchoolSync.Infrastructure.Persistence;

public class HttpAuditContext(IHttpContextAccessor httpContextAccessor) : IAuditContext
{
    public Guid? ActorUserId
    {
        get
        {
            var claim = httpContextAccessor.HttpContext?.User.FindFirst(
                System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    // ASP.NET Core generates HttpContext.TraceIdentifier per request automatically — reusing it
    // rather than minting a second, parallel identifier satisfies company.md §6 with no new
    // moving part, and it's already what appears in the framework's own request logs.
    public string CorrelationId => httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
}
