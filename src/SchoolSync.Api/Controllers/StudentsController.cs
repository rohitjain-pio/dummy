using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Students;

namespace SchoolSync.Api.Controllers;

// API-011 – API-015. Institution scoping is not done here: Student is ITenantOwned, so the
// global query filter (T-002/D4) scopes every read and lookup to the caller's institution_id
// claim. This controller therefore never takes an institution id from the caller for a read —
// only for the create, where a brand-new row needs one assigned.
[ApiController]
[Route("api/v1")]
public class StudentsController(IStudentService service) : ControllerBase
{
    // BR-021/BRL-021: the guardian fields are part of this one body, not a separately-fetchable
    // sub-resource. There is deliberately no GET /students/{id}/guardians — a second endpoint is
    // exactly the "separate, looser rule" BRL-021 forbids.
    public record StudentBody(
        string SrNumber, string RollNumber, string Name, DateOnly DateOfBirth, string Gender, string Email,
        Guid ClassId, string? GuardianFatherName, string? GuardianMotherName,
        string? GuardianContactEmail, string? GuardianPhone);

    public record LinkAccountBody(Guid UserAccountId);

    private Guid CallerInstitutionId => Guid.Parse(User.FindFirst("institution_id")!.Value);

    private static StudentFields ToFields(StudentBody b) => new(
        b.SrNumber, b.RollNumber, b.Name, b.DateOfBirth, b.Gender, b.Email, b.ClassId,
        b.GuardianFatherName, b.GuardianMotherName, b.GuardianContactEmail, b.GuardianPhone);

    private static object ToBody(StudentResult r) => new
    {
        id = r.Id,
        srNumber = r.SrNumber,
        rollNumber = r.RollNumber,
        name = r.Name,
        dateOfBirth = r.DateOfBirth,
        gender = r.Gender,
        email = r.Email,
        classId = r.ClassId,
        guardianFatherName = r.GuardianFatherName,
        guardianMotherName = r.GuardianMotherName,
        guardianContactEmail = r.GuardianContactEmail,
        guardianPhone = r.GuardianPhone,
        isActive = r.IsActive,
        deactivatedAtUtc = r.DeactivatedAtUtc
    };

    // ERR-301 is the only 409 in this controller; ClassNotFound and ValidationFailed both surface
    // as ERR-901, which is what API-011 lists (its error set is exactly {ERR-301, ERR-901}) — a
    // missing class is a bad request field, not a separate catalogued error.
    private IActionResult ToFailure(StudentFailureReason reason) => reason switch
    {
        StudentFailureReason.DuplicateIdentifier => Conflict(ErrCatalogue.Err301),
        StudentFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
        StudentFailureReason.CapacityExceeded => StatusCode(StatusCodes.Status402PaymentRequired, ErrCatalogue.Err801), // CT-503
        _ => BadRequest(ErrCatalogue.Err901)
    };

    // API-011.
    [HttpPost("students")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Enrol([FromBody] StudentBody body, CancellationToken ct)
    {
        var result = await service.EnrolAsync(ToFields(body), CallerInstitutionId, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return StatusCode(StatusCodes.Status201Created, ToBody(result.Value!));
    }

    // API-012.
    [HttpPut("students/{id:guid}")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Amend(Guid id, [FromBody] StudentBody body, CancellationToken ct)
    {
        var result = await service.AmendAsync(id, ToFields(body), ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return Ok(ToBody(result.Value!));
    }

    // API-013.
    [HttpPost("students/{id:guid}/withdraw")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        var result = await service.WithdrawAsync(id, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return Ok(new { id = result.Value!.Id, isActive = result.Value.IsActive, deactivatedAtUtc = result.Value.DeactivatedAtUtc });
    }

    // API-013's mirror — "reversible (/reactivate mirror endpoint exists per the soft-delete
    // pattern)", matching CT-101's institution withdraw/reactivate pair exactly.
    [HttpPost("students/{id:guid}/reactivate")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        var result = await service.ReactivateAsync(id, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return Ok(new { id = result.Value!.Id, isActive = result.Value.IsActive, deactivatedAtUtc = result.Value.DeactivatedAtUtc });
    }

    // API-015.
    [HttpPut("students/{id:guid}/link-account")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> LinkAccount(Guid id, [FromBody] LinkAccountBody body, CancellationToken ct)
    {
        var result = await service.LinkAccountAsync(id, body.UserAccountId, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return Ok(ToBody(result.Value!));
    }

    // API-014 — the three reads. Teacher is included per the contract's "leadership + Teacher
    // (read)"; the write endpoints above are leadership-only, which is BRL-029's read-only
    // Teacher posture applied to student records.
    [HttpGet("students/{id:guid}")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var student = await service.FindAsync(id, ct);
        return student is null ? NotFound(ErrCatalogue.Err902) : Ok(ToBody(student));
    }

    [HttpGet("students")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok((await service.ListAsync(ct)).Select(ToBody));

    [HttpGet("classes/{id:guid}/roster")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> Roster(Guid id, CancellationToken ct) =>
        Ok((await service.RosterAsync(id, ct)).Select(ToBody));
}
