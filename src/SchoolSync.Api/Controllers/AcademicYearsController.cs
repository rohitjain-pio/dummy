using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Configuration;

namespace SchoolSync.Api.Controllers;

// CT-502. API-059/060, plus two endpoints the contracts never define but rollover cannot work
// without (recorded as silences): creating/listing academic years, and setting a class's successor.
[ApiController]
[Route("api/v1")]
[Authorize(Roles = Roles.Leadership)]
public class AcademicYearsController(IRolloverService service) : ControllerBase
{
    public record YearBody(string? Label, DateOnly? StartDate, DateOnly? EndDate);
    public record NextClassBody(Guid? NextClassId);
    public record HoldBackBody(List<Guid>? StudentIds);

    private ConfigCaller Caller => new(User.UserId(), User.Role(), User.InstitutionId());

    private static object ToBody(AcademicYearView y) => new
    {
        id = y.Id,
        label = y.Label,
        startDate = y.StartDate,
        endDate = y.EndDate,
        rolloverRequestedAtUtc = y.RolloverRequestedAtUtc,
        rolledOverAtUtc = y.RolledOverAtUtc,
        nextAcademicYearId = y.NextAcademicYearId
    };

    private IActionResult Failure(RolloverFailureReason reason) => reason switch
    {
        RolloverFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
        RolloverFailureReason.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102),
        RolloverFailureReason.AlreadyRolledOver => Conflict(ErrCatalogue.Err901), // no catalogue code; a year rolls over once
        _ => BadRequest(ErrCatalogue.Err901)
    };

    [HttpPost("academic-years")]
    public async Task<IActionResult> Create([FromBody] YearBody body, CancellationToken ct)
    {
        var result = await service.CreateYearAsync(body.Label, body.StartDate, body.EndDate, Caller, ct);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, ToBody(result.Value!)) : Failure(result.Failure);
    }

    [HttpGet("academic-years")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok((await service.ListYearsAsync(ct)).Select(ToBody));

    [HttpPut("classes/{id:guid}/next-class")]
    public async Task<IActionResult> SetNextClass(Guid id, [FromBody] NextClassBody body, CancellationToken ct)
    {
        var result = await service.SetNextClassAsync(id, body.NextClassId, Caller, ct);
        return result.Succeeded ? Ok(new { classId = id, nextClassId = result.Value }) : Failure(result.Failure);
    }

    // API-059 — 202, async.
    [HttpPost("academic-years/{id:guid}/rollover")]
    public async Task<IActionResult> Rollover(Guid id, CancellationToken ct)
    {
        var result = await service.RequestRolloverAsync(id, Caller, ct);
        return result.Succeeded ? Accepted(ToBody(result.Value!)) : Failure(result.Failure);
    }

    // API-060 — before or after the rollover runs.
    [HttpPost("academic-years/{id:guid}/rollover/hold-back")]
    public async Task<IActionResult> HoldBack(Guid id, [FromBody] HoldBackBody body, CancellationToken ct)
    {
        var result = await service.HoldBackAsync(id, body.StudentIds, Caller, ct);
        return result.Succeeded ? Ok(new { academicYearId = id, heldBack = result.Value }) : Failure(result.Failure);
    }
}
