using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.ParentLinks;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Api.Controllers;

// API-016 – API-019 (T-003). The two GET routes below are `students/me` and `students/linked`,
// which cannot collide with StudentsController's `students/{id:guid}` — the :guid constraint
// means neither literal ever matches that route.
[ApiController]
[Route("api/v1")]
public class ParentLinksController(IParentLinkService service) : ControllerBase
{
    public record ConsumeCodeBody(string Code);

    private Guid CallerUserId => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
    private Guid CallerInstitutionId => Guid.Parse(User.FindFirst("institution_id")!.Value);

    // Status codes come from 08-error-catalogue.md, not from habit: 307 -> 409, 308 -> 410 Gone,
    // 309 -> 403, 310 -> 410 Gone.
    private IActionResult ToFailure(ParentLinkFailureReason reason) => reason switch
    {
        ParentLinkFailureReason.AlreadyConsumed => Conflict(ErrCatalogue.Err307),
        ParentLinkFailureReason.Expired => StatusCode(StatusCodes.Status410Gone, ErrCatalogue.Err308),
        ParentLinkFailureReason.InstitutionMismatch => StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err309),
        ParentLinkFailureReason.NotValid => StatusCode(StatusCodes.Status410Gone, ErrCatalogue.Err310),
        _ => NotFound(ErrCatalogue.Err902)
    };

    // API-016.
    [HttpPost("students/{id:guid}/parent-link-codes")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> IssueCode(Guid id, CancellationToken ct)
    {
        var result = await service.IssueCodeAsync(id, CallerUserId, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);

        // The raw code is returned here and never again — there is no read-back endpoint.
        return StatusCode(StatusCodes.Status201Created, new
        {
            codeId = result.Value!.CodeId,
            code = result.Value.Code,
            expiresAtUtc = result.Value.ExpiresAtUtc
        });
    }

    // API-017.
    [HttpPost("students/{id:guid}/parent-link-codes/{codeId:guid}/revoke")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> RevokeCode(Guid id, Guid codeId, CancellationToken ct)
    {
        var result = await service.RevokeCodeAsync(id, codeId, ct);
        return result.Succeeded ? Ok(new { codeId, revoked = true }) : ToFailure(result.Failure);
    }

    // API-018 — the consumer is the Parent, not staff.
    [HttpPost("parent-links")]
    [Authorize(Roles = nameof(UserRole.Parent))]
    [RequiresPlanCapability(SchoolSync.Application.Commercial.PlanCapability.ParentAccess)] // CT-503 / Q-228
    public async Task<IActionResult> ConsumeCode([FromBody] ConsumeCodeBody body, CancellationToken ct)
    {
        var result = await service.ConsumeCodeAsync(body.Code, CallerUserId, CallerInstitutionId, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);

        return Ok(new { studentId = result.Value!.StudentId, linkedAtUtc = result.Value.LinkedAtUtc });
    }

    // API-019, Student half — FR-074.
    [HttpGet("students/me")]
    [Authorize(Roles = nameof(UserRole.Student))]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var student = await service.FindOwnStudentAsync(CallerUserId, ct);
        return student is null ? NotFound(ErrCatalogue.Err902) : Ok(student);
    }

    // API-019, Parent half — FR-075/BR-066, linked children only.
    [HttpGet("students/linked")]
    [Authorize(Roles = nameof(UserRole.Parent))]
    [RequiresPlanCapability(SchoolSync.Application.Commercial.PlanCapability.ParentAccess)] // CT-503 / Q-228
    public async Task<IActionResult> Linked(CancellationToken ct) =>
        Ok(await service.ListLinkedStudentsAsync(CallerUserId, ct));
}
