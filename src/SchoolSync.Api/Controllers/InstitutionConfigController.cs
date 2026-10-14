using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Configuration;

namespace SchoolSync.Api.Controllers;

// API-056–058 (CT-501). Read: any authenticated user of the institution; write: leadership (BRL-058).
// No request body has an "audit" switch — BRL-059's audit is unconditional.
[ApiController]
[Route("api/v1/institution-config")]
[Authorize]
public class InstitutionConfigController(IInstitutionConfigService service) : ControllerBase
{
    public record GeneralBody(string? AcademicYear, string? TimeZone, string? Language);
    public record NotificationsBody(bool? Email, bool? Sms, bool? InPlatform);
    public record AcademicBody(string? GradingScale, List<string>? CourseCategories, List<string>? AssessmentTypes, string? ReportCardFormat);

    private ConfigCaller Caller => new(User.UserId(), User.Role(), User.InstitutionId());

    private static object ToBody(InstitutionConfigView v) => new
    {
        academicYear = v.AcademicYear,
        timeZone = v.TimeZone,
        language = v.Language,
        notifications = new { email = v.Notifications.Email, sms = v.Notifications.Sms, inPlatform = v.Notifications.InPlatform },
        academic = new { gradingScale = v.GradingScale, courseCategories = v.CourseCategories, assessmentTypes = v.AssessmentTypes, reportCardFormat = v.ReportCardFormat },
        isDefault = v.IsDefault
    };

    private IActionResult Respond(ConfigResult result) => result.Failure switch
    {
        ConfigFailureReason.None => Ok(ToBody(result.Value!)),
        ConfigFailureReason.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102),
        _ => BadRequest(ErrCatalogue.Err901)
    };

    // API-056.
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(ToBody(await service.GetAsync(Caller, ct)));

    [HttpPut]
    public async Task<IActionResult> UpdateGeneral([FromBody] GeneralBody body, CancellationToken ct) =>
        Respond(await service.UpdateGeneralAsync(body.AcademicYear, body.TimeZone, body.Language, Caller, ct));

    // API-057.
    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotifications([FromBody] NotificationsBody body, CancellationToken ct) =>
        Respond(await service.UpdateNotificationsAsync(
            body.Email is { } e && body.Sms is { } s && body.InPlatform is { } i ? new NotificationPreferences(e, s, i) : null, Caller, ct));

    // API-058.
    [HttpPut("academic")]
    public async Task<IActionResult> UpdateAcademic([FromBody] AcademicBody body, CancellationToken ct) =>
        Respond(await service.UpdateAcademicAsync(body.GradingScale, body.CourseCategories, body.AssessmentTypes, body.ReportCardFormat, Caller, ct));
}
