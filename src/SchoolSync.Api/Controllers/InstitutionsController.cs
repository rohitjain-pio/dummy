using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Institutions;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class InstitutionsController(IInstitutionService service, IInstitutionRepository repository, TimeProvider clock) : ControllerBase
{
    public record InstitutionBody(
        string Name, string RegistrationNumber, string ContactEmail, string Telephone,
        string PostalAddress, string PostalCode, string City, string Region);

    private static object ToBody(InstitutionResult r) => new
    {
        id = r.Id,
        name = r.Name,
        registrationNumber = r.RegistrationNumber,
        contactEmail = r.ContactEmail,
        telephone = r.Telephone,
        postalAddress = r.PostalAddress,
        postalCode = r.PostalCode,
        city = r.City,
        region = r.Region,
        isActive = r.IsActive,
        deactivatedAtUtc = r.DeactivatedAtUtc,
        subscriptionPlanId = r.SubscriptionPlanId
    };

    private bool CallerIsPlatformOperator =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value == nameof(UserRole.PlatformOperator);

    private Guid CallerInstitutionId => Guid.Parse(User.FindFirst("institution_id")!.Value);

    // API-001. BRL-001: PlatformOperator only.
    [HttpPost("institutions")]
    [Authorize(Roles = nameof(UserRole.PlatformOperator))]
    public async Task<IActionResult> Register(
        [FromBody] InstitutionBody body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await repository.FindIdempotencyRecordAsync(idempotencyKey, ct);
            if (existing is not null && !existing.IsExpired(clock.GetUtcNow().UtcDateTime))
                return new ContentResult
                {
                    StatusCode = existing.ResponseStatusCode,
                    Content = existing.ResponseBodyJson,
                    ContentType = "application/json"
                };
        }

        var result = await service.RegisterAsync(
            new InstitutionFields(body.Name, body.RegistrationNumber, body.ContactEmail, body.Telephone,
                body.PostalAddress, body.PostalCode, body.City, body.Region), ct);

        if (!result.Succeeded)
            return result.Failure == InstitutionFailureReason.Duplicate
                ? Conflict(ErrCatalogue.Err101)
                : BadRequest(ErrCatalogue.Err901);

        var responseBody = ToBody(result.Value!);

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            await repository.AddIdempotencyRecordAsync(new Domain.Entities.IdempotencyRecord
            {
                Key = idempotencyKey,
                ResponseBodyJson = JsonSerializer.Serialize(responseBody),
                ResponseStatusCode = StatusCodes.Status201Created,
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime
            }, ct);
            await repository.SaveChangesAsync(ct);
        }

        return StatusCode(StatusCodes.Status201Created, responseBody);
    }

    // API-002. BRL-002.
    [HttpPut("institutions/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] InstitutionBody body, CancellationToken ct)
    {
        var result = await service.UpdateAsync(
            id, new InstitutionFields(body.Name, body.RegistrationNumber, body.ContactEmail, body.Telephone,
                body.PostalAddress, body.PostalCode, body.City, body.Region),
            CallerInstitutionId, CallerIsPlatformOperator, ct);

        if (!result.Succeeded)
            return result.Failure == InstitutionFailureReason.NotFound
                ? NotFound(ErrCatalogue.Err902)
                : StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102);

        return Ok(ToBody(result.Value!));
    }

    // API-004. PlatformOperator only.
    [HttpPost("institutions/{id:guid}/withdraw")]
    [Authorize(Roles = nameof(UserRole.PlatformOperator))]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        var result = await service.WithdrawAsync(id, ct);
        if (!result.Succeeded) return NotFound(ErrCatalogue.Err902);
        return Ok(new { id = result.Value!.Id, isActive = result.Value.IsActive, deactivatedAtUtc = result.Value.DeactivatedAtUtc });
    }

    // API-004b. PlatformOperator only.
    [HttpPost("institutions/{id:guid}/reactivate")]
    [Authorize(Roles = nameof(UserRole.PlatformOperator))]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        var result = await service.ReactivateAsync(id, ct);
        if (!result.Succeeded) return NotFound(ErrCatalogue.Err902);
        return Ok(new { id = result.Value!.Id, isActive = result.Value.IsActive, deactivatedAtUtc = result.Value.DeactivatedAtUtc });
    }
}

// API-005: anonymous, public — a separate controller/route so [Authorize] never applies here
// even by accident (no shared base controller with the tenant-scoped Institutions endpoints).
[ApiController]
[Route("api/v1/public")]
public class PublicInstitutionDirectoryController(IInstitutionService service) : ControllerBase
{
    [HttpGet("institutions")]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string? query, CancellationToken ct)
    {
        var results = await service.SearchDirectoryAsync(query, ct);
        // Shape is exactly { id, name, city, region } per DirectoryEntry (id: Q-235) — never contactEmail/
        // postalAddress/registrationNumber (NFR-016).
        return Ok(results.Select(r => new { id = r.Id, name = r.Name, city = r.City, region = r.Region }));
    }
}
