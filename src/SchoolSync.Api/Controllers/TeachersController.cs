using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Teachers;

namespace SchoolSync.Api.Controllers;

// API-020 – API-025. Every endpoint here is LEADERSHIP ONLY, including the reads: API-023 says
// "leadership only (matches the mockup's own gating, no BRD grant to Teacher for a peer directory
// — ui-design-final.md §5)". That is the deliberate difference from CT-102's student reads, which
// do include Teacher, and from API-028's class reads, which include Teacher per FR-027.
[ApiController]
[Route("api/v1")]
[Authorize(Roles = Roles.Leadership)]
public class TeachersController(ITeacherService service) : ControllerBase
{
    public record TeacherBody(
        string Name, string ContactEmail, string Telephone, string Gender, string Address, DateOnly JoiningDate);

    public record LinkAccountBody(Guid UserAccountId);
    public record ClassTeacherBody(Guid? TeacherId);
    public record SubjectAssignmentBody(Guid TeacherId, Guid ClassId);

    private Guid CallerInstitutionId => Guid.Parse(User.FindFirst("institution_id")!.Value);

    private static TeacherFields ToFields(TeacherBody b) =>
        new(b.Name, b.ContactEmail, b.Telephone, b.Gender, b.Address, b.JoiningDate);

    private static object ToBody(TeacherResult r) => new
    {
        id = r.Id,
        name = r.Name,
        contactEmail = r.ContactEmail,
        telephone = r.Telephone,
        gender = r.Gender,
        address = r.Address,
        joiningDate = r.JoiningDate,
        isActive = r.IsActive,
        deactivatedAtUtc = r.DeactivatedAtUtc
    };

    private IActionResult ToFailure(TeacherFailureReason reason) => reason switch
    {
        TeacherFailureReason.AssignmentConflict => Conflict(ErrCatalogue.Err303),
        TeacherFailureReason.HasActiveAssignments => Conflict(ErrCatalogue.Err304),
        TeacherFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
        _ => BadRequest(ErrCatalogue.Err901)
    };

    // API-020.
    [HttpPost("teachers")]
    public async Task<IActionResult> Register([FromBody] TeacherBody body, CancellationToken ct)
    {
        var result = await service.RegisterAsync(ToFields(body), CallerInstitutionId, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return StatusCode(StatusCodes.Status201Created, ToBody(result.Value!));
    }

    // API-021.
    [HttpPut("teachers/{id:guid}")]
    public async Task<IActionResult> Amend(Guid id, [FromBody] TeacherBody body, CancellationToken ct)
    {
        var result = await service.AmendAsync(id, ToFields(body), ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : ToFailure(result.Failure);
    }

    // API-022. Soft-delete only (SPEC-FR-019) — there is no hard-delete path.
    [HttpPost("teachers/{id:guid}/remove")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        var result = await service.RemoveAsync(id, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return Ok(new { id = result.Value!.Id, isActive = result.Value.IsActive, deactivatedAtUtc = result.Value.DeactivatedAtUtc });
    }

    [HttpPost("teachers/{id:guid}/reinstate")]
    public async Task<IActionResult> Reinstate(Guid id, CancellationToken ct)
    {
        var result = await service.ReinstateAsync(id, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return Ok(new { id = result.Value!.Id, isActive = result.Value.IsActive, deactivatedAtUtc = result.Value.DeactivatedAtUtc });
    }

    // SPEC-FR-023 — the teacher mirror of CT-102's API-015.
    [HttpPut("teachers/{id:guid}/link-account")]
    public async Task<IActionResult> LinkAccount(Guid id, [FromBody] LinkAccountBody body, CancellationToken ct)
    {
        var result = await service.LinkAccountAsync(id, body.UserAccountId, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : ToFailure(result.Failure);
    }

    // API-023 — leadership only, per the contract's explicit note.
    [HttpGet("teachers/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var teacher = await service.FindAsync(id, ct);
        return teacher is null ? NotFound(ErrCatalogue.Err902) : Ok(ToBody(teacher));
    }

    [HttpGet("teachers")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok((await service.ListAsync(ct)).Select(ToBody));

    // API-024. A null teacherId releases the current class teacher (BRL-023's "until the first is
    // released"); a non-null one assigns, subject to both clauses of BRL-023.
    [HttpPut("classes/{id:guid}/class-teacher")]
    public async Task<IActionResult> SetClassTeacher(Guid id, [FromBody] ClassTeacherBody body, CancellationToken ct)
    {
        var result = await service.SetClassTeacherAsync(id, body.TeacherId, ct);
        return result.Succeeded ? Ok(new { classId = id, classTeacherId = body.TeacherId }) : ToFailure(result.Failure);
    }

    // API-025.
    [HttpPost("subjects/{id:guid}/teacher-assignments")]
    public async Task<IActionResult> AssignSubject(Guid id, [FromBody] SubjectAssignmentBody body, CancellationToken ct)
    {
        var result = await service.AssignSubjectAsync(id, body.TeacherId, body.ClassId, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);

        return StatusCode(StatusCodes.Status201Created, new
        {
            subjectId = result.Value!.SubjectId,
            teacherId = result.Value.TeacherId,
            classId = result.Value.ClassId
        });
    }
}
