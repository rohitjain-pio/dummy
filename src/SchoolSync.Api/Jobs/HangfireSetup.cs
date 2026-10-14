using Hangfire;
using SchoolSync.Application.Operations;
using SchoolSync.Infrastructure.Operations;

namespace SchoolSync.Api.Jobs;

// T-005 / Q-222: Hangfire in-process with SQL Server storage. Everything here is decided LAZILY from
// the live configuration — never read before the host is built (the CT-002 lesson) — so a host with
// Hangfire:Enabled=false never touches job storage at all.
public static class HangfireSetup
{
    public static bool IsEnabled(IConfiguration configuration) => configuration.GetValue("Hangfire:Enabled", true);

    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services)
    {
        services.AddHangfire((sp, config) => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(sp.GetRequiredService<IConfiguration>().GetConnectionString("SchoolSync"))
            .UseFilter(new ReportRunFailureFilter(sp.GetRequiredService<IServiceScopeFactory>()))); // TC-604

        services.AddSingleton<IReportJobQueue>(sp => IsEnabled(sp.GetRequiredService<IConfiguration>())
            ? new HangfireReportJobQueue(sp.GetRequiredService<IBackgroundJobClient>())
            : ActivatorUtilities.CreateInstance<DisabledReportJobQueue>(sp));

        services.AddSingleton<SchoolSync.Application.Configuration.IRolloverQueue>(sp => IsEnabled(sp.GetRequiredService<IConfiguration>())
            ? new SchoolSync.Infrastructure.Configuration.HangfireRolloverQueue(sp.GetRequiredService<IBackgroundJobClient>())
            : ActivatorUtilities.CreateInstance<SchoolSync.Infrastructure.Configuration.DisabledRolloverQueue>(sp));

        services.AddHostedService<ConditionalHangfireServer>();
        return services;
    }
}

// CT-501: the platform's daily recurring jobs (Tech Design §3.5). AddOrUpdate is idempotent, so
// every app instance may call it at startup. Kept separate so the schedule is unit-testable.
public static class RecurringJobs
{
    public static void Register(IRecurringJobManager jobs)
    {
        jobs.AddOrUpdate<SchoolSync.Infrastructure.Configuration.AttendanceLockSweepJob>(
            SchoolSync.Infrastructure.Configuration.RecurringJobIds.AttendanceLockSweep, j => j.RunAsync(CancellationToken.None), Cron.Daily());
        jobs.AddOrUpdate<SchoolSync.Infrastructure.Configuration.RetentionSweepJob>(
            SchoolSync.Infrastructure.Configuration.RecurringJobIds.RetentionSweep, j => j.RunAsync(CancellationToken.None), Cron.Daily());
        jobs.AddOrUpdate<SchoolSync.Infrastructure.Configuration.YearEndRolloverJob>(
            SchoolSync.Infrastructure.Configuration.YearEndRolloverJob.Id, j => j.RunAsync(CancellationToken.None), Cron.Daily());
        jobs.AddOrUpdate<SchoolSync.Infrastructure.Commercial.TrialReversionJob>( // A4: same scheduler as the rollover
            SchoolSync.Infrastructure.Commercial.TrialReversionJob.Id, j => j.RunAsync(CancellationToken.None), Cron.Daily());
    }
}

// Starts the in-process Hangfire server only when enabled (AddHangfireServer would resolve storage
// unconditionally at startup).
public sealed class ConditionalHangfireServer(IServiceProvider sp, IConfiguration configuration) : IHostedService, IDisposable
{
    private BackgroundJobServer? _server;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!HangfireSetup.IsEnabled(configuration)) return Task.CompletedTask;

        // Resolving IGlobalConfiguration applies the AddHangfire configuration above — the DI-scoped
        // job activator, the storage, and ReportRunFailureFilter — which the server then picks up.
        _ = sp.GetRequiredService<IGlobalConfiguration>();
        _server = new BackgroundJobServer(new BackgroundJobServerOptions(), sp.GetRequiredService<JobStorage>());
        RecurringJobs.Register(sp.GetRequiredService<IRecurringJobManager>());
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_server is not null)
        {
            _server.SendStop();
            await _server.WaitForShutdownAsync(cancellationToken);
        }
    }

    public void Dispose() => _server?.Dispose();
}
