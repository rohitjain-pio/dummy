using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Operations;

// API-050–052 (CT-402). BRL-051, VAL-021/022, Q-226.
public class ReportService(IReportRunRepository runs, IReportJobQueue queue, IReportFileStore files, TimeProvider clock) : IReportService
{
    private static ReportRunView ToView(ReportRun r) => new(
        r.Id, r.ReportType, r.Format, r.FromDate, r.ToDate, r.ScopeClassId, r.Status, r.GeneratedByUserId,
        r.RequestedAtUtc, r.GeneratedAtUtc, r.FailureReason);

    public async Task<ReportResult<ReportRunView>> RequestAsync(ReportRequest request, OperationsCaller caller, CancellationToken ct)
    {
        static ReportResult<ReportRunView> Fail(ReportFailureReason r) => ReportResult<ReportRunView>.Fail(r);

        if (!caller.IsLeadership && caller.Role != UserRole.Teacher) return Fail(ReportFailureReason.Forbidden); // FR-054

        // VAL-021: both dates, from <= to. VAL-022 + Q-226: CSV is the only format this build can produce;
        // "PDF" is a named, recorded deviation and is refused rather than silently served as CSV.
        if (!Enum.TryParse<ReportType>(request.ReportType, ignoreCase: false, out var type) || !Enum.IsDefined(type)
            || request.From is not { } from || request.To is not { } to || from > to
            || !string.Equals(request.Format, "CSV", StringComparison.OrdinalIgnoreCase))
            return Fail(ReportFailureReason.ValidationFailed);

        if (request.ScopeClassId is { } classId && !await runs.ClassExistsAsync(classId, ct)) return Fail(ReportFailureReason.NotFound);

        Guid? scopeTeacher = null;
        if (!caller.IsLeadership) // BRL-051 / TC-603: a Teacher's reports cover their own classes only
        {
            if (type == ReportType.TeacherPerformance)
            {
                if (request.ScopeClassId is not null) return Fail(ReportFailureReason.Forbidden);
                scopeTeacher = caller.UserId; // their own performance, never a colleague's
            }
            else if (request.ScopeClassId is not { } own || !await runs.TeacherTeachesClassAsync(caller.UserId, own, ct))
            {
                return Fail(ReportFailureReason.Forbidden); // whole-school, or a class they do not teach
            }
        }

        var run = new ReportRun
        {
            Id = Guid.NewGuid(),
            InstitutionId = caller.InstitutionId,
            ReportType = type,
            Format = ReportFormat.Csv,
            FromDate = from,
            ToDate = to,
            ScopeClassId = request.ScopeClassId,
            ScopeTeacherUserId = scopeTeacher,
            Status = ReportRunStatus.Pending,
            GeneratedByUserId = caller.UserId,
            RequestedAtUtc = clock.GetUtcNow().UtcDateTime
        };
        await runs.AddAsync(run, ct);
        await runs.SaveChangesAsync(ct); // PF-010 step 1: the Pending row exists before the job is queued
        await queue.EnqueueAsync(run.Id, ct);
        return ReportResult<ReportRunView>.Ok(ToView(run));
    }

    // FR-055: retained indefinitely. Leadership sees every run; a Teacher sees the runs they requested
    // (all of which are scoped to their own classes by construction above).
    public async Task<IReadOnlyList<ReportRunView>> ListAsync(OperationsCaller caller, CancellationToken ct) =>
        (await runs.ListAsync(caller.IsLeadership ? null : caller.UserId, ct)).Select(ToView).ToList();

    private async Task<ReportRun?> FindVisibleAsync(Guid id, OperationsCaller caller, CancellationToken ct)
    {
        var run = await runs.FindAsync(id, ct);
        return run is not null && (caller.IsLeadership || run.GeneratedByUserId == caller.UserId) ? run : null;
    }

    public async Task<ReportResult<ReportRunView>> FindAsync(Guid id, OperationsCaller caller, CancellationToken ct) =>
        await FindVisibleAsync(id, caller, ct) is { } run
            ? ReportResult<ReportRunView>.Ok(ToView(run))
            : ReportResult<ReportRunView>.Fail(ReportFailureReason.NotFound);

    public async Task<ReportResult<ReportFile>> DownloadAsync(Guid id, OperationsCaller caller, CancellationToken ct)
    {
        var run = await FindVisibleAsync(id, caller, ct);
        if (run is null) return ReportResult<ReportFile>.Fail(ReportFailureReason.NotFound);
        if (run.Status != ReportRunStatus.Ready || run.BlobUri is null) return ReportResult<ReportFile>.Fail(ReportFailureReason.NotReady);

        var fileName = $"{run.ReportType}-{run.FromDate:yyyyMMdd}-{run.ToDate:yyyyMMdd}.csv";
        return ReportResult<ReportFile>.Ok(new ReportFile(fileName, "text/csv", await files.OpenAsync(run.BlobUri, ct)));
    }
}
