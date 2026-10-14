using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Classes;
using SchoolSync.Application.Teachers;

namespace SchoolSync.Api.Controllers;

// API-026 – API-028. BRL-029 is the shape of this controller: the READ actions admit Teacher,
// every WRITE action is leadership-only, so "Teacher role: read-only, 403 on write" is a property
// of the routing table rather than a check someone has to remember inside each method.
[ApiController]
[Route("api/v1")]
public class ClassesController(IClassService service) : ControllerBase
{
    public record ClassBody(string Name, string Section);

    private Guid CallerInstitutionId => Guid.Parse(User.FindFirst("institution_id")!.Value);

    private static object ToBody(ClassResult r) => new
    {
        id = r.Id,
        name = r.Name,
        section = r.Section,
        classTeacherId = r.ClassTeacherId
    };

    private IActionResult ToFailure(ClassFailureReason reason) => reason switch
    {
        ClassFailureReason.DuplicateNameSection => Conflict(ErrCatalogue.Err305),
        ClassFailureReason.StudentsStillPlaced => Conflict(ErrCatalogue.Err306),
        ClassFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
        _ => BadRequest(ErrCatalogue.Err901)
    };

    // API-026.
    [HttpPost("classes")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Create([FromBody] ClassBody body, CancellationToken ct)
    {
        var result = await service.CreateClassAsync(body.Name, body.Section, CallerInstitutionId, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return StatusCode(StatusCodes.Status201Created, ToBody(result.Value!));
    }

    // API-027 (amend).
    [HttpPut("classes/{id:guid}")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ClassBody body, CancellationToken ct)
    {
        var result = await service.UpdateClassAsync(id, body.Name, body.Section, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : ToFailure(result.Failure);
    }

    // API-027 (remove). Blocked while students remain placed (BRL-028).
    [HttpDelete("classes/{id:guid}")]
    [Authorize(Roles = Roles.Leadership)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await service.DeleteClassAsync(id, ct);
        return result.Succeeded ? NoContent() : ToFailure(result.Failure);
    }

    // API-028 — Teacher included (FR-027's correction to the mockup's nav exclusion). This is the
    // gate CT-105 is measured by: "Contract test confirms Teacher role reaches GET /classes".
    [HttpGet("classes/{id:guid}")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var @class = await service.FindClassAsync(id, ct);
        return @class is null ? NotFound(ErrCatalogue.Err902) : Ok(ToBody(@class));
    }

    [HttpGet("classes")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok((await service.ListClassesAsync(ct)).Select(ToBody));
}

// API-029 and API-030. Leadership only throughout (BRL-029).
[ApiController]
[Route("api/v1")]
[Authorize(Roles = Roles.Leadership)]
public class SubjectsController(IClassService classService, ITeacherService teacherService) : ControllerBase
{
    public record SubjectBody(string Name);

    // API-030's payload. It carries a teacherId as well as a classId because the data model has
    // exactly one mapping table — SubjectTeacherClassMapping, composite PK
    // (subject_id, teacher_id, class_id) — and SPEC-FR-029 itself reads "Map subjects to teachers
    // AND classes". There is no subject↔class relation that omits the teacher. See the build log:
    // this makes API-030 and API-025 two routes onto the same mapping, which is recorded as a
    // spec finding rather than resolved by inventing a second table.
    public record ClassMappingBody(Guid TeacherId, Guid ClassId);

    private Guid CallerInstitutionId => Guid.Parse(User.FindFirst("institution_id")!.Value);

    private IActionResult ToFailure(ClassFailureReason reason) => reason switch
    {
        ClassFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
        _ => BadRequest(ErrCatalogue.Err901)
    };

    // API-029.
    [HttpPost("subjects")]
    public async Task<IActionResult> Create([FromBody] SubjectBody body, CancellationToken ct)
    {
        var result = await classService.CreateSubjectAsync(body.Name, CallerInstitutionId, ct);
        if (!result.Succeeded) return ToFailure(result.Failure);
        return StatusCode(StatusCodes.Status201Created, new
        {
            id = result.Value!.Id,
            name = result.Value.Name
        });
    }

    [HttpPut("subjects/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SubjectBody body, CancellationToken ct)
    {
        var result = await classService.UpdateSubjectAsync(id, body.Name, ct);
        return result.Succeeded
            ? Ok(new { id = result.Value!.Id, name = result.Value.Name })
            : ToFailure(result.Failure);
    }

    [HttpDelete("subjects/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await classService.DeleteSubjectAsync(id, ct);
        return result.Succeeded ? NoContent() : ToFailure(result.Failure);
    }

    [HttpGet("subjects")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok((await classService.ListSubjectsAsync(ct)).Select(s => new { id = s.Id, name = s.Name }));

    // API-030. Delegates to the same service method API-025 uses rather than duplicating the
    // mapping logic — one behaviour, reachable by both documented routes.
    [HttpPost("subjects/{id:guid}/class-mappings")]
    public async Task<IActionResult> MapToClass(Guid id, [FromBody] ClassMappingBody body, CancellationToken ct)
    {
        var result = await teacherService.AssignSubjectAsync(id, body.TeacherId, body.ClassId, ct);
        if (!result.Succeeded) return NotFound(ErrCatalogue.Err902);

        return StatusCode(StatusCodes.Status201Created, new
        {
            subjectId = result.Value!.SubjectId,
            teacherId = result.Value.TeacherId,
            classId = result.Value.ClassId
        });
    }
}
