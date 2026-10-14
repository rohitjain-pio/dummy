using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Common;
using SchoolSync.Application.Commercial;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Api.Controllers;

// CT-503: API-061/062 (public) and API-064 + the Q-227 plan editor (PlatformOperator).
[ApiController]
[Route("api/v1")]
public class SubscriptionsController(ISubscriptionService service, IIdempotencyStore idempotency) : ControllerBase
{
    public record AssignBody(Guid? InstitutionId, Guid? SubscriptionPlanId, DateOnly? StartDate);
    public record PlanBody(int? LearnerCapacity, int? StorageAllowanceGb, string? SupportLevel, List<string>? Capabilities);
    public record EnquiryBody(string? FullName, string? Email, string? Institution, string? StudentCountRange, string? Message);

    private IActionResult Failure(CommercialFailureReason reason) => reason switch
    {
        CommercialFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
        CommercialFailureReason.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102),
        _ => BadRequest(ErrCatalogue.Err901)
    };

    private static object ToBody(PlanView p) => new
    {
        id = p.Id,
        name = p.Name,
        learnerCapacity = p.LearnerCapacity == int.MaxValue ? (int?)null : p.LearnerCapacity, // null = unlimited
        storageAllowanceGb = p.StorageAllowanceGb,
        supportLevel = p.SupportLevel,
        capabilities = p.Capabilities
    };

    // API-061 — anonymous; structural fields only (no marketing copy).
    [HttpGet("public/plans")]
    [AllowAnonymous]
    public async Task<IActionResult> Plans(CancellationToken ct) => Ok((await service.ListPlansAsync(ct)).Select(ToBody));

    // API-062 — anonymous. Idempotency-Key per the contract (scoped with an empty institution id).
    [HttpPost("public/enquiries")]
    [AllowAnonymous]
    public async Task<IActionResult> Enquire([FromBody] EnquiryBody body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(idempotencyKey) && await idempotency.FindAsync("enquiries", Guid.Empty, idempotencyKey, ct) is { } stored)
            return new ContentResult { StatusCode = stored.StatusCode, Content = stored.BodyJson, ContentType = "application/json" };

        var result = await service.SubmitEnquiryAsync(new Enquiry(body.FullName, body.Email, body.Institution, body.StudentCountRange, body.Message), ct);
        if (!result.Succeeded) return Failure(result.Failure);

        var response = new { received = true };
        if (!string.IsNullOrEmpty(idempotencyKey))
            await idempotency.SaveAsync("enquiries", Guid.Empty, idempotencyKey, new StoredResponse(StatusCodes.Status201Created, JsonSerializer.Serialize(response, JsonSerializerOptions.Web)), ct);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    // API-064 — PlatformOperator only (BRL-064), re-verified against the database in the service.
    [HttpPut("subscriptions")]
    [Authorize(Roles = nameof(UserRole.PlatformOperator))]
    public async Task<IActionResult> Assign([FromBody] AssignBody body, CancellationToken ct)
    {
        var result = await service.AssignAsync(body.InstitutionId, body.SubscriptionPlanId, body.StartDate, User.UserId(), ct);
        return result.Succeeded
            ? Ok(new { institutionId = result.Value!.InstitutionId, subscriptionPlanId = result.Value.SubscriptionPlanId, startDate = result.Value.SubscriptionStartDate })
            : Failure(result.Failure);
    }

    // Q-227 — the tier limits become data a PlatformOperator maintains.
    [HttpPut("subscription-plans/{id:guid}")]
    [Authorize(Roles = nameof(UserRole.PlatformOperator))]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] PlanBody body, CancellationToken ct)
    {
        var result = await service.UpdatePlanAsync(id, new PlanFields(body.LearnerCapacity, body.StorageAllowanceGb, body.SupportLevel, body.Capabilities), User.UserId(), ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : Failure(result.Failure);
    }
}
