using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Application.Common;
using SchoolSync.Application.Communication;

namespace SchoolSync.Api.Controllers;

// API-041–045. BRL-040: every route requires authentication and nothing more at the attribute level;
// passcode, membership and creator-or-leadership are decided in RoomService.
[ApiController]
[Route("api/v1/rooms")]
[Authorize]
public class RoomsController(
    IRoomService service, IMessagingService messaging, IMeetingService meetings, IIdempotencyStore idempotency, IRoomEventPublisher events) : ControllerBase
{
    public record CreateRoomBody(string? Name, string? Description, string? Passcode);
    public record JoinBody(string? Passcode);
    public record ReviseBody(string? Content);

    private CommunicationCaller Caller => new(User.UserId(), User.Role(), User.InstitutionId());

    // Built from RoomResult, which has no passcode member: TC-501 is enforced by shape.
    private static object ToBody(RoomResult r) => new { id = r.Id, name = r.Name, description = r.Description, createdByUserId = r.CreatedByUserId };

    internal static object ToBody(MessageView m) => new
    {
        id = m.Id,
        roomId = m.RoomId,
        authorId = m.AuthorId,
        content = m.Content,
        sentAtUtc = m.SentAtUtc,
        isWithdrawn = m.IsWithdrawn,
        revisedAtUtc = m.RevisedAtUtc
    };

    // API-041. The passcode goes in and is never echoed back, not even in this 201.
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomBody body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(idempotencyKey)
            && await idempotency.FindAsync("rooms", User.InstitutionId(), idempotencyKey, ct) is { } stored)
            return new ContentResult { StatusCode = stored.StatusCode, Content = stored.BodyJson, ContentType = "application/json" };

        var result = await service.CreateAsync(body.Name, body.Description, body.Passcode, Caller, ct);
        if (!result.Succeeded) return AnnouncementsController.Failure(this, result.Failure);

        var responseBody = ToBody(result.Value!);
        if (!string.IsNullOrEmpty(idempotencyKey))
            await idempotency.SaveAsync("rooms", User.InstitutionId(), idempotencyKey,
                new StoredResponse(StatusCodes.Status201Created, JsonSerializer.Serialize(responseBody, JsonSerializerOptions.Web)), ct);
        return StatusCode(StatusCodes.Status201Created, responseBody);
    }

    // API-042.
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok((await service.ListAsync(ct)).Select(ToBody));

    // API-043. Q-219: success records membership; the client then connects to the hub.
    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> Join(Guid id, [FromBody] JoinBody body, CancellationToken ct)
    {
        var result = await service.JoinAsync(id, body.Passcode, Caller, ct);
        if (!result.Succeeded) return AnnouncementsController.Failure(this, result.Failure);

        if (result.Value!.NewlyJoined) await events.ParticipantJoinedAsync(id, User.UserId(), ct); // EVT-004 "joined"
        return Ok(new { roomId = result.Value.RoomId, hubPath = result.Value.HubPath });
    }

    // API-047 — room members only (BRL-046). The API issues a token and never relays media.
    [HttpPost("{id:guid}/meetings/token")]
    public async Task<IActionResult> MeetingToken(Guid id, CancellationToken ct)
    {
        var result = await meetings.IssueTokenAsync(id, Caller, ct);
        return result.Succeeded
            ? Ok(new { acsToken = result.Value!.AcsToken, expiresAtUtc = result.Value.ExpiresAtUtc, groupCallId = result.Value.GroupCallId })
            : AnnouncementsController.Failure(this, result.Failure);
    }

    // API-046 — revise. Author only (BRL-044): 403 ERR-102 otherwise; ERR-504 over CFG-003.
    [HttpPut("{id:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> ReviseMessage(Guid id, Guid messageId, [FromBody] ReviseBody body, CancellationToken ct)
    {
        var result = await messaging.ReviseAsync(id, messageId, body.Content, Caller, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : AnnouncementsController.Failure(this, result.Failure);
    }

    // API-046 — withdraw (soft). The row stays; content is blanked on read (TC-510).
    [HttpDelete("{id:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> WithdrawMessage(Guid id, Guid messageId, CancellationToken ct)
    {
        var result = await messaging.WithdrawAsync(id, messageId, Caller, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : AnnouncementsController.Failure(this, result.Failure);
    }

    // API-044.
    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id, CancellationToken ct)
    {
        var result = await service.HistoryAsync(id, Caller, ct);
        return result.Succeeded ? Ok(result.Value!.Select(ToBody)) : AnnouncementsController.Failure(this, result.Failure);
    }

    // API-045.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        var result = await service.RemoveAsync(id, Caller, ct);
        return result.Succeeded ? NoContent() : AnnouncementsController.Failure(this, result.Failure);
    }
}
