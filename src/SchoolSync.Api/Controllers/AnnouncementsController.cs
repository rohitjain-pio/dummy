using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Common;
using SchoolSync.Application.Communication;

namespace SchoolSync.Api.Controllers;

// API-037–040. Withdraw deliberately carries only [Authorize], not a role attribute: BRL-038's
// denial must be 403 WITH the ERR-501 body (TC-505), which a role attribute cannot produce.
[ApiController]
[Route("api/v1/announcements")]
public class AnnouncementsController(IAnnouncementService service, IIdempotencyStore idempotency) : ControllerBase
{
    public record AnnouncementBody(string? Title, string? Content, DateOnly? EffectiveDate);

    private CommunicationCaller Caller => new(User.UserId(), User.Role(), User.InstitutionId());

    internal static object ToBody(AnnouncementResult a) => new
    {
        id = a.Id,
        title = a.Title,
        content = a.Content,
        effectiveDate = a.EffectiveDate,
        authorId = a.AuthorId,
        createdAtUtc = a.CreatedAtUtc,
        isWithdrawn = a.IsWithdrawn,
        withdrawnAtUtc = a.WithdrawnAtUtc
    };

    internal static IActionResult Failure(ControllerBase c, CommunicationFailureReason reason) => reason switch
    {
        CommunicationFailureReason.NotFound => c.NotFound(ErrCatalogue.Err902),
        CommunicationFailureReason.Forbidden => c.StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102),
        CommunicationFailureReason.WithdrawLeadershipOnly => c.StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err501),
        CommunicationFailureReason.WrongPasscode => c.StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err502),
        CommunicationFailureReason.RoomRemovalForbidden => c.StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err503),
        CommunicationFailureReason.MessageTooLong => c.BadRequest(ErrCatalogue.Err504),
        CommunicationFailureReason.NotRoomMember => c.StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err505),
        CommunicationFailureReason.MeetingsUnavailable => c.StatusCode(StatusCodes.Status503ServiceUnavailable, ErrCatalogue.Err903),
        _ => c.BadRequest(ErrCatalogue.Err901)
    };

    // API-037.
    [HttpPost]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> Create(
        [FromBody] AnnouncementBody body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(idempotencyKey)
            && await idempotency.FindAsync("announcements", User.InstitutionId(), idempotencyKey, ct) is { } stored)
            return new ContentResult { StatusCode = stored.StatusCode, Content = stored.BodyJson, ContentType = "application/json" };

        var result = await service.CreateAsync(new AnnouncementFields(body.Title, body.Content, body.EffectiveDate), Caller, ct);
        if (!result.Succeeded) return Failure(this, result.Failure);

        var responseBody = ToBody(result.Value!);
        if (!string.IsNullOrEmpty(idempotencyKey))
            await idempotency.SaveAsync("announcements", User.InstitutionId(), idempotencyKey,
                new StoredResponse(StatusCodes.Status201Created, JsonSerializer.Serialize(responseBody, JsonSerializerOptions.Web)), ct);
        return StatusCode(StatusCodes.Status201Created, responseBody);
    }

    // API-038.
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.LeadershipAndTeacher)]
    public async Task<IActionResult> Amend(Guid id, [FromBody] AnnouncementBody body, CancellationToken ct)
    {
        var result = await service.AmendAsync(id, new AnnouncementFields(body.Title, body.Content, body.EffectiveDate), Caller, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : Failure(this, result.Failure);
    }

    // API-039.
    [HttpPost("{id:guid}/withdraw")]
    [Authorize]
    public async Task<IActionResult> Withdraw(Guid id, CancellationToken ct)
    {
        var result = await service.WithdrawAsync(id, Caller, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : Failure(this, result.Failure);
    }

    // API-040 — any authenticated user of the institution (BR-036; the tenant filter scopes it).
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> List(CancellationToken ct) => Ok((await service.ListCurrentAsync(ct)).Select(ToBody));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        await service.FindAsync(id, ct) is { } a ? Ok(ToBody(a)) : NotFound(ErrCatalogue.Err902);
}
