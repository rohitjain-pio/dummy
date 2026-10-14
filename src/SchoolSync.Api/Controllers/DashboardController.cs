using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Operations;

namespace SchoolSync.Api.Controllers;

// API-048 / API-049 (CT-401).
[ApiController]
[Route("api/v1")]
public class DashboardController(IDashboardService service, SchoolSync.Application.Commercial.IPlanEntitlements entitlements) : ControllerBase
{
    // API-048 — one route, role-conditional payload (UI-015). Members a variant must not see are
    // omitted from the JSON entirely (null + WhenWritingNull), not sent as zero.
    [HttpGet("dashboard")]
    [Authorize]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        // CT-503 / Q-228: the Parent variant shares this route with every other role, so the
        // ParentAccess tier check is applied here for the Parent role only, not as a route filter.
        if (User.Role() == SchoolSync.Domain.Entities.UserRole.Parent
            && !await entitlements.IncludesAsync(User.InstitutionId(), SchoolSync.Application.Commercial.PlanCapability.ParentAccess, ct))
            return RequiresPlanCapabilityAttribute.PlanCapabilityDenied();

        var result = await service.GetAsync(new OperationsCaller(User.UserId(), User.Role(), User.InstitutionId()), ct);
        if (result is null) return StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102);

        return new JsonResult(result, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        });
    }

    // API-049 — anonymous, aggregate-only (BR-049).
    [HttpGet("platform/summary")]
    [AllowAnonymous]
    public async Task<IActionResult> PlatformSummary(CancellationToken ct)
    {
        var s = await service.GetPlatformSummaryAsync(ct);
        return Ok(new { institutions = s.Institutions, students = s.Students, teachers = s.Teachers, classes = s.Classes });
    }
}
