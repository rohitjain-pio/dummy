using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Attendance;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Api.Controllers;

// 05-api-contracts/attendance.md. The role attribute admits the broadest set an endpoint allows;
// the narrower rules (BRL-033 staff-attendance-is-leadership-only, a Teacher's own class,
// SPEC-FR-033's original recorder) need data, so they are decided in AttendanceService and come
// back as Forbidden -> 403 ERR-102, matching InstitutionsController's BRL-002 scope denial.
[ApiController]
[Route("api/v1")]
public class AttendanceController(IAttendanceService service) : ControllerBase
{
    public record MarkEntryBody(Guid PersonId, string? PersonType, string? Outcome);
    public record MarkBody(Guid? ClassId, DateOnly Date, List<MarkEntryBody>? Entries);

    private AttendanceCaller Caller => new(
        Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!),
        Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role)!),
        Guid.Parse(User.FindFirst("institution_id")!.Value));

    private IActionResult ToFailure(AttendanceFailureReason reason) => reason switch
    {
        AttendanceFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
        AttendanceFailureReason.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102),
        AttendanceFailureReason.Locked => StatusCode(StatusCodes.Status423Locked, ErrCatalogue.Err401),
        AttendanceFailureReason.NeedsApproval => UnprocessableEntity(ErrCatalogue.Err402),
        AttendanceFailureReason.FutureDate => BadRequest(ErrCatalogue.Err403),
        _ => BadRequest(ErrCatalogue.Err901)
    };

    // API-031. Route moved from the contract's GET /classes/{id}/roster, which API-014 (CT-102)
    // already owns — decided by Rashmi Mehta, 2026-09-14 (Q-217); spec correction pending.
    [HttpGet("attendance/roster")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> GetRoster([FromQuery] Guid classId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var result = await service.GetRosterAsync(classId, date, Caller, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);

        // currentOutcome is null for NotYetMarked (BRL-034) — serialized as null, never "Absent".
        return Ok(result.Value!.Select(r => new
        {
            studentId = r.StudentId,
            name = r.Name,
            currentOutcome = r.CurrentOutcome?.ToString()
        }));
    }

    public record CorrectionRequestBody(string? RequestedOutcome, string? Reason);
    public record RejectBody(string? Reason);

    private static object ToBody(CorrectionResult r) => new
    {
        id = r.Id,
        attendanceRecordId = r.AttendanceRecordId,
        requestedOutcome = r.RequestedOutcome.ToString(),
        status = r.Status.ToString(),
        recordOutcome = r.RecordOutcome.ToString(),
        reason = r.Reason,
        resolutionNote = r.ResolutionNote
    };

    // API-033. 201 for every request created — Pending (a Teacher on day 1-30) or AppliedDirectly
    // (day 0 by the original recorder, or leadership, Q-205); `status` tells the caller which.
    [HttpPost("attendance/{id:guid}/correction-requests")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> RequestCorrection(Guid id, [FromBody] CorrectionRequestBody body, CancellationToken ct)
    {
        var result = await service.RequestCorrectionAsync(id, body.RequestedOutcome, body.Reason, Caller, ct);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, ToBody(result.Value!)) : ToFailure(result.Failure);
    }

    // API-034 — approver roles per Q-204: Administrator, Principal, SchoolInCharge.
    [HttpPost("attendance/correction-requests/{id:guid}/approve")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var result = await service.ApproveCorrectionAsync(id, Caller, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : ToFailure(result.Failure);
    }

    [HttpPost("attendance/correction-requests/{id:guid}/reject")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBody body, CancellationToken ct)
    {
        var result = await service.RejectCorrectionAsync(id, body.Reason, Caller, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : ToFailure(result.Failure);
    }

    private static object ToBody(AttendanceView v) => new
    {
        id = v.Id,
        personId = v.PersonId,
        personType = v.PersonType.ToString(),
        attendanceDate = v.AttendanceDate,
        outcome = v.Outcome.ToString(),
        recordedByUserId = v.RecordedByUserId,
        recordedAtUtc = v.RecordedAtUtc,
        correctionStatus = v.CorrectionStatus.ToString(),
        locked = v.Locked
    };

    // API-035 — leadership only.
    [HttpDelete("attendance/{id:guid}")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        var result = await service.RemoveAsync(id, Caller, ct);
        return result.Succeeded ? NoContent() : ToFailure(result.Failure);
    }

    // API-036 — leadership (institution-wide) + Teacher (own class; classId required).
    [HttpGet("attendance")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> Search([FromQuery] Guid? personId, [FromQuery] Guid? classId, [FromQuery] DateOnly? date, CancellationToken ct)
    {
        var result = await service.SearchAsync(new AttendanceQuery(personId, classId, date), Caller, ct);
        return result.Succeeded ? Ok(result.Value!.Select(ToBody)) : ToFailure(result.Failure);
    }

    // API-036 narrower routes — the API-019 pattern. Literal segments, so neither can match the
    // {id:guid} routes above.
    [HttpGet("attendance/me")]
    [Authorize(Roles = nameof(UserRole.Student))]
    public async Task<IActionResult> Mine([FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok((await service.ForOwnStudentAsync(date, Caller, ct)).Select(ToBody));

    [HttpGet("attendance/linked")]
    [Authorize(Roles = nameof(UserRole.Parent))]
    [RequiresPlanCapability(SchoolSync.Application.Commercial.PlanCapability.ParentAccess)] // CT-503 / Q-228
    public async Task<IActionResult> Linked([FromQuery] DateOnly? date, CancellationToken ct) =>
        Ok((await service.ForLinkedStudentsAsync(date, Caller, ct)).Select(ToBody));

    // API-032.
    [HttpPut("attendance")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> Mark([FromBody] MarkBody body, CancellationToken ct)
    {
        var entries = (body.Entries ?? []).Select(e => new MarkEntry(e.PersonId, e.PersonType, e.Outcome)).ToList();
        var result = await service.MarkAsync(new MarkRequest(body.ClassId, body.Date, entries), Caller, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);

        return Ok(result.Value!.Select(e => new
        {
            recordId = e.RecordId,
            personId = e.PersonId,
            personType = e.PersonType.ToString(),
            outcome = e.Outcome.ToString(),
            result = e.Result.ToString()
        }));
    }
}
