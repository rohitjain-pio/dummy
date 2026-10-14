using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Operations;

namespace SchoolSync.Api.Controllers;

// API-053–055 (CT-403). Read for any authenticated user; maintain for leadership (BRL-056).
[ApiController]
[Route("api/v1/timetable")]
public class TimetableController(ITimetableService service) : ControllerBase
{
    public record PeriodBody(Guid? ClassId, Guid? SubjectId, Guid? TeacherId, string? Room, string? DayOfWeek, string? TimeSlot);

    private OperationsCaller Caller => new(User.UserId(), User.Role(), User.InstitutionId());

    private static object ToBody(PeriodView p) => new
    {
        id = p.Id,
        classId = p.ClassId,
        subjectId = p.SubjectId,
        teacherId = p.TeacherId,
        room = p.Room,
        dayOfWeek = p.DayOfWeek.ToString(),
        timeSlot = p.TimeSlot,
        isPublished = p.IsPublished,
        publishedAtUtc = p.PublishedAtUtc
    };

    private static ErrorResponse ToError(ScheduleConflict c) => c switch
    {
        ScheduleConflict.Teacher => ErrCatalogue.Err701,
        ScheduleConflict.Class => ErrCatalogue.Err702,
        _ => ErrCatalogue.Err703
    };

    private IActionResult Failure<T>(TimetableResult<T> result) => result.Failure switch
    {
        // TC-608: every conflict found, in one 409. The body is {errors:[{code,message}...]} — each
        // entry is a verbatim catalogue body — because one save can trip up to three ERR- codes.
        TimetableFailureReason.Conflict => Conflict(new { errors = result.Conflicts!.Select(ToError) }),
        TimetableFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
        TimetableFailureReason.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102),
        _ => BadRequest(ErrCatalogue.Err901)
    };

    private static PeriodFields ToFields(PeriodBody b) => new(b.ClassId, b.SubjectId, b.TeacherId, b.Room, b.DayOfWeek, b.TimeSlot);

    // API-053.
    [HttpPost]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Create([FromBody] PeriodBody body, CancellationToken ct)
    {
        var result = await service.CreateAsync(ToFields(body), Caller, ct);
        return result.Succeeded ? StatusCode(StatusCodes.Status201Created, ToBody(result.Value!)) : Failure(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Update(Guid id, [FromBody] PeriodBody body, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, ToFields(body), Caller, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : Failure(result);
    }

    // API-054 — {id} is a period id (Q-225).
    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await service.PublishAsync(id, Caller, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : Failure(result);
    }

    // API-055.
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List([FromQuery] Guid? classId, [FromQuery] Guid? teacherId, CancellationToken ct) =>
        Ok((await service.ListAsync(classId, teacherId, Caller, ct)).Select(ToBody));
}
