using Azure.Storage.Blobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolSync.Application.Operations;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Operations;

// Tenant-filtered (request path).
public class ReportRunRepository(SchoolSyncDbContext db) : IReportRunRepository
{
    public async Task<bool> TeacherTeachesClassAsync(Guid userId, Guid classId, CancellationToken ct)
    {
        var teacherId = await db.UserAccounts.Where(u => u.Id == userId && u.IsActive).Select(u => u.TeacherId).FirstOrDefaultAsync(ct);
        return teacherId is { } t
               && (await db.Classes.AnyAsync(c => c.Id == classId && c.ClassTeacherId == t, ct)
                   || await db.SubjectTeacherClassMappings.AnyAsync(m => m.ClassId == classId && m.TeacherId == t, ct));
    }

    public Task<bool> ClassExistsAsync(Guid classId, CancellationToken ct) => db.Classes.AnyAsync(c => c.Id == classId, ct);
    public async Task AddAsync(ReportRun run, CancellationToken ct) => await db.ReportRuns.AddAsync(run, ct);
    public Task<ReportRun?> FindAsync(Guid id, CancellationToken ct) => db.ReportRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<ReportRun>> ListAsync(Guid? onlyRequestedBy, CancellationToken ct) =>
        await db.ReportRuns.Where(r => onlyRequestedBy == null || r.GeneratedByUserId == onlyRequestedBy)
            .OrderByDescending(r => r.RequestedAtUtc).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

// Tenant-filtered: only ever used inside a job's BackgroundTenantScope (ADR-0007).
public class ReportDataSource(SchoolSyncDbContext db) : IReportDataSource
{
    public Task<string?> InstitutionTimeZoneAsync(CancellationToken ct) =>
        db.InstitutionConfigurations.Select(c => (string?)c.TimeZone).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ClassLabel>> ClassesAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct) =>
        await db.Classes.Where(c => onlyClassIds == null || onlyClassIds.Contains(c.Id))
            .Select(c => new ClassLabel(c.Id, c.Name, c.Section)).ToListAsync(ct);

    public async Task<IReadOnlyList<StudentLabel>> StudentsAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct) =>
        await db.Students.Where(s => onlyClassIds == null || (s.ClassId != null && onlyClassIds.Contains(s.ClassId.Value)))
            .Select(s => new StudentLabel(s.Id, s.Name, s.SrNumber, s.ClassId)).ToListAsync(ct);

    public async Task<IReadOnlyList<SubjectClasses>> SubjectsAsync(IReadOnlyCollection<Guid>? onlyClassIds, CancellationToken ct)
    {
        var mappings = await db.SubjectTeacherClassMappings
            .Where(m => onlyClassIds == null || onlyClassIds.Contains(m.ClassId))
            .Select(m => new { m.SubjectId, m.ClassId }).ToListAsync(ct);
        var subjectIds = mappings.Select(m => m.SubjectId).Distinct().ToList();
        var names = await db.Subjects.Where(s => subjectIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        return mappings.GroupBy(m => m.SubjectId)
            .Select(g => new SubjectClasses(g.Key, names.GetValueOrDefault(g.Key, ""), g.Select(m => m.ClassId).Distinct().ToList())).ToList();
    }

    public async Task<IReadOnlyList<TeacherLabel>> TeachersAsync(Guid? onlyTeacherUserId, Guid? onlyClassId, CancellationToken ct)
    {
        var teachers = await db.Teachers.Where(t => t.IsActive).Select(t => new { t.Id, t.Name }).ToListAsync(ct);
        var accounts = await db.UserAccounts.Where(u => u.TeacherId != null).Select(u => new { u.Id, TeacherId = u.TeacherId!.Value }).ToListAsync(ct);
        var classTeacher = await db.Classes.Where(c => c.ClassTeacherId != null).Select(c => new { c.Id, TeacherId = c.ClassTeacherId!.Value }).ToListAsync(ct);
        var mapped = await db.SubjectTeacherClassMappings.Select(m => new { m.ClassId, m.TeacherId }).ToListAsync(ct);

        return teachers.Select(t =>
            {
                var userId = accounts.FirstOrDefault(a => a.TeacherId == t.Id)?.Id;
                var classIds = classTeacher.Where(c => c.TeacherId == t.Id).Select(c => c.Id)
                    .Union(mapped.Where(m => m.TeacherId == t.Id).Select(m => m.ClassId)).ToList();
                return new TeacherLabel(t.Id, t.Name, userId, classIds);
            })
            .Where(t => onlyTeacherUserId == null || t.UserAccountId == onlyTeacherUserId)
            .Where(t => onlyClassId == null || t.ClassIds.Contains(onlyClassId.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<AttendanceRow>> StudentAttendanceAsync(IReadOnlyCollection<Guid> studentIds, DateOnly from, DateOnly to, CancellationToken ct) =>
        await db.AttendanceRecords
            .Where(r => r.PersonType == PersonType.Student && studentIds.Contains(r.PersonId) && r.AttendanceDate >= from && r.AttendanceDate <= to)
            .Select(r => new AttendanceRow(r.PersonId, r.AttendanceDate, r.Outcome, r.RecordedByUserId, r.RecordedAtUtc))
            .ToListAsync(ct);
}

// PF-010 steps 2–3, independent of the job framework so it is callable from Hangfire and from tests.
public class ReportJobRunner(SchoolSyncDbContext db, IReportDataSource data, IReportFileStore files, TimeProvider clock)
{
    public async Task RunAsync(Guid reportRunId, CancellationToken ct)
    {
        // ADR-0007: the job's only cross-tenant read — its own run, by id. Everything after this line
        // runs inside that run's institution scope, under the ordinary tenant filter.
        var run = await db.ReportRuns.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == reportRunId, ct)
            ?? throw new InvalidOperationException($"ReportRun {reportRunId} does not exist.");
        if (run.Status != ReportRunStatus.Pending) return; // a retry after success, or after Failed, does nothing

        using var _ = BackgroundTenantScope.Enter(run.InstitutionId);
        var output = await new ReportGenerator(data).GenerateAsync(run, ct);
        run.BlobUri = await files.SaveAsync(run.Id, output.FileName, output.Content, ct);
        run.Status = ReportRunStatus.Ready;
        run.GeneratedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }
}

// TC-604 / NFR-011: the one place a run becomes Failed. Audited (ReportRun is IAuditable) and logged
// at Error, which is what the ops alerting channel consumes (company.md observability defaults).
public class ReportFailureRecorder(SchoolSyncDbContext db, ILogger<ReportFailureRecorder> logger)
{
    public async Task MarkFailedAsync(Guid reportRunId, string reason, CancellationToken ct)
    {
        var run = await db.ReportRuns.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == reportRunId, ct); // ADR-0007
        logger.LogError("Report generation failed permanently for ReportRun {ReportRunId} ({ReportType}, institution {InstitutionId}): {Reason}",
            reportRunId, run?.ReportType, run?.InstitutionId, reason);
        if (run is null || run.Status != ReportRunStatus.Pending) return;

        using var _ = BackgroundTenantScope.Enter(run.InstitutionId);
        run.Status = ReportRunStatus.Failed;
        run.FailureReason = reason.Length > 1000 ? reason[..1000] : reason;
        run.GeneratedAtUtc = null;
        await db.SaveChangesAsync(ct);
    }
}

// Marks a job whose first argument is a ReportRun id, so the failure filter knows which run to fail.
[AttributeUsage(AttributeTargets.Method)]
public sealed class ReportRunJobAttribute : Attribute;

// The Hangfire entry point. Three automatic retries with Hangfire's backoff, then Failed (Q-222).
public class ReportJob(ReportJobRunner runner)
{
    public const int Attempts = 3;

    [ReportRunJob]
    [AutomaticRetry(Attempts = Attempts, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public Task RunAsync(Guid reportRunId, CancellationToken ct) => runner.RunAsync(reportRunId, ct);
}

// Runs when Hangfire actually applies FailedState — which, under AutomaticRetry, happens only after
// the last attempt. Earlier failures are elected to ScheduledState and never reach here.
public class ReportRunFailureFilter(IServiceScopeFactory scopes) : IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState failed) return;
        var job = context.BackgroundJob.Job;
        if (job?.Method.GetCustomAttributes(typeof(ReportRunJobAttribute), inherit: true).Length is not > 0) return;
        if (job.Args.FirstOrDefault() is not Guid reportRunId) return;

        using var scope = scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<ReportFailureRecorder>()
            .MarkFailedAsync(reportRunId, failed.Exception?.Message ?? failed.Reason ?? "Report generation failed.", CancellationToken.None)
            .GetAwaiter().GetResult(); // Hangfire's filter contract is synchronous
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction) { }
}

public class HangfireReportJobQueue(IBackgroundJobClient jobs) : IReportJobQueue
{
    public Task EnqueueAsync(Guid reportRunId, CancellationToken ct)
    {
        jobs.Enqueue<ReportJob>(job => job.RunAsync(reportRunId, CancellationToken.None));
        return Task.CompletedTask;
    }
}

// Used only where Hangfire is switched off (Hangfire:Enabled=false — the HTTP test hosts). Runs stay
// Pending; tests that need generation register their own queue.
public class DisabledReportJobQueue(ILogger<DisabledReportJobQueue> logger) : IReportJobQueue
{
    public Task EnqueueAsync(Guid reportRunId, CancellationToken ct)
    {
        logger.LogWarning("Background jobs are disabled; ReportRun {ReportRunId} will remain Pending.", reportRunId);
        return Task.CompletedTask;
    }
}

// D11 / Q-222 — Azure Blob Storage (CFG-SEC-05), when configured.
public class BlobReportFileStore(BlobServiceClient blobs) : IReportFileStore
{
    private const string Container = "reports";

    public async Task<string> SaveAsync(Guid reportRunId, string fileName, byte[] content, CancellationToken ct)
    {
        var container = blobs.GetBlobContainerClient(Container);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = container.GetBlobClient($"{reportRunId:N}/{fileName}");
        await blob.UploadAsync(new BinaryData(content), overwrite: true, ct);
        return blob.Uri.ToString();
    }

    public async Task<Stream> OpenAsync(string uri, CancellationToken ct)
    {
        var name = new BlobUriBuilder(new Uri(uri)).BlobName;
        return await blobs.GetBlobContainerClient(Container).GetBlobClient(name).OpenReadAsync(cancellationToken: ct);
    }
}

// Q-222 — local disk when Blob Storage is not configured. Development and tests only.
public class LocalDiskReportFileStore(string rootDirectory) : IReportFileStore
{
    public async Task<string> SaveAsync(Guid reportRunId, string fileName, byte[] content, CancellationToken ct)
    {
        var directory = Path.Combine(rootDirectory, reportRunId.ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Path.GetFileName(fileName));
        await File.WriteAllBytesAsync(path, content, ct);
        return new Uri(path).AbsoluteUri;
    }

    public Task<Stream> OpenAsync(string uri, CancellationToken ct)
    {
        var path = Path.GetFullPath(new Uri(uri).LocalPath);
        if (!path.StartsWith(Path.GetFullPath(rootDirectory), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Report file is outside the report store."); // never read an arbitrary path
        return Task.FromResult<Stream>(File.OpenRead(path));
    }
}
