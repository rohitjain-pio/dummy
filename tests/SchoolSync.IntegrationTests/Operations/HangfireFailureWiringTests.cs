using System.Reflection;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Operations;
using SchoolSync.Infrastructure.Persistence;
using Xunit;

namespace SchoolSync.IntegrationTests.Operations;

// TC-604's Hangfire half: a REAL Hangfire server (in-memory storage, owned by this test only) runs
// a report-run job that always throws, retries per its AutomaticRetry attribute, and — only when
// the attempts are exhausted — ReportRunFailureFilter marks the ReportRun Failed. Isolated in its
// own non-parallel collection because Hangfire's job filters are process-global.
[CollectionDefinition(nameof(HangfireIsolation), DisableParallelization = true)]
public class HangfireIsolation;

public static class AlwaysFailingReportJob
{
    public static int Attempts;

    [ReportRunJob]
    [AutomaticRetry(Attempts = 1, DelaysInSeconds = [1], OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public static void Run(Guid reportRunId)
    {
        Interlocked.Increment(ref Attempts);
        throw new InvalidOperationException("Simulated generation failure");
    }
}

[Collection(nameof(HangfireIsolation))]
public class HangfireFailureWiringTests
{
    // Hangfire's log provider is process-global. Install one this class owns, so a provider left
    // behind by some other (now disposed) host can never break these tests — the failure mode that
    // surfaced in the first full P4 run.
    public HangfireFailureWiringTests() =>
        Hangfire.Logging.LogProvider.SetCurrentLogProvider(new Hangfire.Logging.LogProviders.ColouredConsoleLogProvider(Hangfire.Logging.LogLevel.Warn));

    [Fact] // The production job carries the retry policy and the marker the failure filter keys on
    public void ReportJob_declares_three_retries_then_fail_and_is_marked_as_a_report_run_job()
    {
        var method = typeof(ReportJob).GetMethod(nameof(ReportJob.RunAsync))!;
        var retry = method.GetCustomAttribute<AutomaticRetryAttribute>()!;
        Assert.Equal(3, retry.Attempts);
        Assert.Equal(AttemptsExceededAction.Fail, retry.OnAttemptsExceeded);
        Assert.NotNull(method.GetCustomAttribute<ReportRunJobAttribute>());
    }

    [Fact]
    public async Task A_real_Hangfire_job_that_exhausts_its_retries_marks_the_report_run_Failed()
    {
        var institutionId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenant, HttpCurrentTenant>(); // no HttpContext: the background scope path
        services.AddDbContext<SchoolSyncDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<ReportFailureRecorder>();
        await using var provider = services.BuildServiceProvider();

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
            db.ReportRuns.Add(new ReportRun
            {
                Id = runId,
                InstitutionId = institutionId,
                ReportType = ReportType.ClassAttendance,
                Format = ReportFormat.Csv,
                FromDate = new DateOnly(2026, 9, 1),
                ToDate = new DateOnly(2026, 9, 7),
                Status = ReportRunStatus.Pending,
                GeneratedByUserId = Guid.NewGuid(),
                RequestedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var storage = new InMemoryStorage();
        var filter = new ReportRunFailureFilter(provider.GetRequiredService<IServiceScopeFactory>());
        GlobalJobFilters.Filters.Add(filter);
        AlwaysFailingReportJob.Attempts = 0;
        try
        {
            using var server = new BackgroundJobServer(new BackgroundJobServerOptions { SchedulePollingInterval = TimeSpan.FromMilliseconds(500), WorkerCount = 1 }, storage);
            new BackgroundJobClient(storage).Enqueue(() => AlwaysFailingReportJob.Run(runId));

            var deadline = DateTime.UtcNow.AddSeconds(30);
            ReportRunStatus status;
            do
            {
                await Task.Delay(250);
                using var scope = provider.CreateScope();
                status = (await scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>().ReportRuns.IgnoreQueryFilters().SingleAsync(r => r.Id == runId)).Status;
            } while (status == ReportRunStatus.Pending && DateTime.UtcNow < deadline);

            Assert.Equal(ReportRunStatus.Failed, status);
            Assert.Equal(2, AlwaysFailingReportJob.Attempts); // first try + 1 retry, and only then Failed
        }
        finally
        {
            GlobalJobFilters.Filters.Remove(filter);
        }
    }
}
