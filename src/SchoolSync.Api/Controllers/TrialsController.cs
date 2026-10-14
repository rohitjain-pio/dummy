using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Common;
using SchoolSync.Application.Commercial;

namespace SchoolSync.Api.Controllers;

// API-063 (CT-504). Q-230: authenticated, own institution (or a PlatformOperator). The body carries
// ONLY institutionId — there is no payment field to accept (FR-070).
[ApiController]
[Route("api/v1/trials")]
[Authorize]
public class TrialsController(ITrialService service, IIdempotencyStore idempotency) : ControllerBase
{
    public record TrialBody(Guid? InstitutionId);

    [HttpPost]
    public async Task<IActionResult> Start([FromBody] TrialBody body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(idempotencyKey) && await idempotency.FindAsync("trials", User.InstitutionId(), idempotencyKey, ct) is { } stored)
            return new ContentResult { StatusCode = stored.StatusCode, Content = stored.BodyJson, ContentType = "application/json" };

        var result = await service.StartAsync(body.InstitutionId, User.UserId(), User.Role(), User.InstitutionId(), ct);
        if (!result.Succeeded)
            return result.Failure switch
            {
                TrialFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
                TrialFailureReason.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102),
                TrialFailureReason.NotEligible => Conflict(ErrCatalogue.Err901), // already trialled, or not on Entry
                _ => BadRequest(ErrCatalogue.Err901)
            };

        var response = new { institutionId = result.Value!.InstitutionId, trialPlanId = result.Value.TrialPlanId, trialExpiresAtUtc = result.Value.TrialExpiresAtUtc };
        if (!string.IsNullOrEmpty(idempotencyKey))
            await idempotency.SaveAsync("trials", User.InstitutionId(), idempotencyKey, new StoredResponse(StatusCodes.Status201Created, JsonSerializer.Serialize(response, JsonSerializerOptions.Web)), ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
