using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Authorization;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Common;
using SchoolSync.Application.Operations;

namespace SchoolSync.Api.Controllers;

// API-050–052 (CT-402). Role breadth at the attribute; BRL-051's own-class scope is in ReportService.
[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = Roles.LeadershipAndTeacher)]
public class ReportsController(IReportService service, IIdempotencyStore idempotency) : ControllerBase
{
    public record DateRangeBody(DateOnly? From, DateOnly? To);
    public record ScopeBody(Guid? ClassId);
    public record ReportBody(string? ReportType, DateRangeBody? DateRange, ScopeBody? Scope, string? Format);

    private OperationsCaller Caller => new(User.UserId(), User.Role(), User.InstitutionId());

    private static object ToBody(ReportRunView r) => new
    {
        id = r.Id,
        reportType = r.ReportType.ToString(),
        format = r.Format.ToString().ToUpperInvariant(),
        dateRange = new { from = r.From, to = r.To },
        scope = new { classId = r.ScopeClassId },
        status = r.Status.ToString(),
        requestedByUserId = r.RequestedByUserId,
        requestedAtUtc = r.RequestedAtUtc,
        generatedAtUtc = r.GeneratedAtUtc,
        failureReason = r.FailureReason
    };

    private IActionResult Failure(ReportFailureReason reason) => reason switch
    {
        ReportFailureReason.NotFound => NotFound(ErrCatalogue.Err902),
        ReportFailureReason.Forbidden => StatusCode(StatusCodes.Status403Forbidden, ErrCatalogue.Err102),
        ReportFailureReason.NotReady => Conflict(ErrCatalogue.Err901), // no catalogue code for "not ready yet"
        _ => BadRequest(ErrCatalogue.Err901)
    };

    // API-050 — 202 Accepted with the run id (A1). Idempotency-Key replays the original run.
    [HttpPost]
    public async Task<IActionResult> RequestReport(
        [FromBody] ReportBody body, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(idempotencyKey)
            && await idempotency.FindAsync("reports", User.InstitutionId(), idempotencyKey, ct) is { } stored)
            return new ContentResult { StatusCode = stored.StatusCode, Content = stored.BodyJson, ContentType = "application/json" };

        var result = await service.RequestAsync(
            new ReportRequest(body.ReportType, body.DateRange?.From, body.DateRange?.To, body.Scope?.ClassId, body.Format), Caller, ct);
        if (!result.Succeeded) return Failure(result.Failure);

        var responseBody = new { reportRunId = result.Value!.Id };
        if (!string.IsNullOrEmpty(idempotencyKey))
            await idempotency.SaveAsync("reports", User.InstitutionId(), idempotencyKey,
                new StoredResponse(StatusCodes.Status202Accepted, JsonSerializer.Serialize(responseBody, JsonSerializerOptions.Web)), ct);
        return Accepted(responseBody);
    }

    // API-051.
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok((await service.ListAsync(Caller, ct)).Select(ToBody));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await service.FindAsync(id, Caller, ct);
        return result.Succeeded ? Ok(ToBody(result.Value!)) : Failure(result.Failure);
    }

    // API-052 — streams the stored file.
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var result = await service.DownloadAsync(id, Caller, ct);
        return result.Succeeded ? File(result.Value!.Content, result.Value.ContentType, result.Value.FileName) : Failure(result.Failure);
    }
}
