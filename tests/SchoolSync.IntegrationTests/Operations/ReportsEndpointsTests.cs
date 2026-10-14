using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolSync.Application.Operations;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Operations;
using SchoolSync.IntegrationTests.Attendance;
using Xunit;

namespace SchoolSync.IntegrationTests.Operations;

// CT-402 — API-050–052 through the HTTP pipeline, with a job queue that runs the real
// ReportJobRunner synchronously in a fresh DI scope (no HttpContext, so the job's
// BackgroundTenantScope path is genuinely exercised) and applies the same retry-then-fail rule as
// the Hangfire job. HangfireFailureWiringTests proves Hangfire itself reaches the same failure path.
public class ReportsEndpointsTests : IAsyncLifetime
{
    // Runs the job like Hangfire would: ReportJob.Attempts retries after the first try, then Failed.
    private sealed class SynchronousReportJobQueue(IServiceScopeFactory scopes) : IReportJobQueue
    {
        // The request's HttpContext lives in an AsyncLocal that would otherwise flow into the "job"
        // and hand it the caller's tenant claim. Suppressing flow gives the work what a real
        // Hangfire worker has: no HttpContext, so HttpCurrentTenant must use BackgroundTenantScope.
        public Task EnqueueAsync(Guid reportRunId, CancellationToken ct)
        {
            Task work;
            using (ExecutionContext.SuppressFlow()) work = Task.Run(() => RunLikeAJobAsync(reportRunId));
            return work;
        }

        private async Task RunLikeAJobAsync(Guid reportRunId)
        {
            Exception? last = null;
            for (var attempt = 0; attempt <= ReportJob.Attempts; attempt++)
            {
                using var scope = scopes.CreateScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<ReportJobRunner>().RunAsync(reportRunId, CancellationToken.None);
                    return;
                }
                catch (Exception ex) { last = ex; }
            }
            using var failScope = scopes.CreateScope();
            await failScope.ServiceProvider.GetRequiredService<ReportFailureRecorder>().MarkFailedAsync(reportRunId, last!.Message, CancellationToken.None);
        }
    }

    private sealed class BrokenFileStore : IReportFileStore
    {
        public int Attempts;
        public Task<string> SaveAsync(Guid id, string name, byte[] content, CancellationToken ct) { Interlocked.Increment(ref Attempts); throw new IOException("Blob storage unavailable"); }
        public Task<Stream> OpenAsync(string uri, CancellationToken ct) => throw new IOException("unavailable");
    }

    private sealed class ReportsFactory(bool runJobs, IReportFileStore? fileStore) : AttendanceApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                if (runJobs)
                {
                    services.RemoveAll<IReportJobQueue>();
                    services.AddSingleton<IReportJobQueue>(sp => new SynchronousReportJobQueue(sp.GetRequiredService<IServiceScopeFactory>()));
                }
                services.RemoveAll<IReportFileStore>();
                services.AddSingleton(fileStore ?? new LocalDiskReportFileStore(Path.Combine(Path.GetTempPath(), "schoolsync-report-tests", Guid.NewGuid().ToString("N"))));
            });
        }
    }

    private AttendanceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId, _classA, _classB, _teacherUserId, _asha;
    private static readonly DateOnly Mon = new(2026, 9, 7);

    public Task InitializeAsync() => SetUpAsync(new ReportsFactory(runJobs: true, fileStore: null));

    private async Task SetUpAsync(AttendanceApiFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        _classA = await _factory.SeedClassAsync(_institutionId, "Grade 5", "A");
        _classB = await _factory.SeedClassAsync(_institutionId, "Grade 6", "B");
        var (teacherId, teacherUserId) = await _factory.SeedTeacherWithAccountAsync(_institutionId);
        _teacherUserId = teacherUserId;
        await _factory.MakeClassTeacherAsync(_classA, teacherId);
        _asha = await _factory.SeedStudentAsync(_institutionId, _classA, "SR-1", "R-1");
        await _factory.SeedRecordAsync(_institutionId, _asha, Mon, AttendanceOutcome.Present, teacherUserId);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, _institutionId));

    private static object Body(string type = "StudentPerformance", Guid? classId = null, string format = "CSV", string from = "2026-09-07", string to = "2026-09-13") =>
        new { reportType = type, dateRange = new { from, to }, scope = new { classId }, format };

    private async Task<Guid> RequestAsync(object body, HttpStatusCode expected = HttpStatusCode.Accepted)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/reports", body);
        Assert.Equal(expected, response.StatusCode);
        return expected == HttpStatusCode.Accepted ? (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reportRunId").GetGuid() : Guid.Empty;
    }

    [Fact] // PF-010 end to end: 202 -> Ready -> the downloaded CSV holds the computed row
    public async Task Leadership_requests_a_report_which_becomes_ready_and_downloads_as_csv()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);

        var runId = await RequestAsync(Body());

        var run = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{runId}");
        Assert.Equal("Ready", run.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, run.GetProperty("generatedAtUtc").ValueKind); // FR-055 last-generated time

        var download = await _client.GetAsync($"/api/v1/reports/{runId}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("text/csv", download.Content.Headers.ContentType!.MediaType);
        var csv = await download.Content.ReadAsStringAsync();
        Assert.Contains("Seeded Student,SR-1,1,0,0,0,100.0", csv);

        Assert.Contains((await _client.GetFromJsonAsync<List<JsonElement>>("/api/v1/reports"))!, r => r.GetProperty("id").GetGuid() == runId);
    }

    [Fact] // TC-603 — adversarial: a Teacher asking for a class not their own is denied, and so is whole-school
    public async Task A_teacher_cannot_request_a_report_for_another_class_or_the_whole_school()
    {
        AuthenticateAs(_teacherUserId, UserRole.Teacher);

        await RequestAsync(Body(classId: _classB), HttpStatusCode.Forbidden);
        await RequestAsync(Body(classId: null), HttpStatusCode.Forbidden);
        await RequestAsync(Body(type: "TeacherPerformance", classId: _classB), HttpStatusCode.Forbidden);
        Assert.Equal(0, await _factory.QueryAsync(db => db.ReportRuns.IgnoreQueryFilters().CountAsync()));

        await RequestAsync(Body(classId: _classA)); // positive control: their own class
    }

    [Fact] // BR-051: a Teacher's history shows only their own runs; leadership sees all
    public async Task Report_history_is_scoped_for_teachers_and_complete_for_leadership()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        var principalRun = await RequestAsync(Body(type: "ClassAttendance"));
        AuthenticateAs(_teacherUserId, UserRole.Teacher);
        var teacherRun = await RequestAsync(Body(classId: _classA));

        var teacherList = (await _client.GetFromJsonAsync<List<JsonElement>>("/api/v1/reports"))!;
        Assert.Equal([teacherRun], teacherList.Select(r => r.GetProperty("id").GetGuid()));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/reports/{principalRun}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/v1/reports/{principalRun}/download")).StatusCode);

        AuthenticateAs(Guid.NewGuid(), UserRole.Administrator);
        Assert.Equal(2, (await _client.GetFromJsonAsync<List<JsonElement>>("/api/v1/reports"))!.Count);
    }

    [Theory] // VAL-021 / VAL-022 / Q-226: PDF is refused rather than silently produced as CSV
    [InlineData("StudentPerformance", "PDF", "2026-09-07", "2026-09-13")]
    [InlineData("StudentPerformance", "CSV", "2026-09-13", "2026-09-07")]
    [InlineData("StudentSatisfaction", "CSV", "2026-09-07", "2026-09-13")]
    public async Task Invalid_report_requests_are_rejected_with_ERR_901(string type, string format, string from, string to)
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        var response = await _client.PostAsJsonAsync("/api/v1/reports", Body(type, _classA, format, from, to));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ERR-901", await response.Content.ReadAsStringAsync());
    }

    [Fact] // API-050 idempotency: a retried POST with the same key returns the same run, no second job
    public async Task A_repeated_Idempotency_Key_returns_the_original_report_run()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        var key = Guid.NewGuid().ToString();

        async Task<Guid> PostAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/reports") { Content = JsonContent.Create(Body()) };
            request.Headers.Add("Idempotency-Key", key);
            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reportRunId").GetGuid();
        }

        Assert.Equal(await PostAsync(), await PostAsync());
        Assert.Equal(1, await _factory.QueryAsync(db => db.ReportRuns.IgnoreQueryFilters().CountAsync()));
    }

    [Fact] // Students and Parents have no report access (FR-054 names leadership + Teacher)
    public async Task Students_and_parents_cannot_request_or_list_reports()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Student);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/api/v1/reports", Body(classId: _classA))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/reports")).StatusCode);
    }

    [Fact] // A run that is still Pending cannot be downloaded yet
    public async Task Downloading_a_pending_report_is_a_409()
    {
        await DisposeAsync();
        await SetUpAsync(new AttendanceApiFactory()); // Hangfire disabled, no job runner: the run stays Pending
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);

        var runId = await RequestAsync(Body());

        Assert.Equal("Pending", (await _client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{runId}")).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await _client.GetAsync($"/api/v1/reports/{runId}/download")).StatusCode);
    }

    [Fact] // TC-604 — adversarial: every attempt fails -> Failed with a reason and an audit row, never silently dropped
    public async Task When_every_attempt_fails_the_run_is_marked_Failed_and_audited()
    {
        await DisposeAsync();
        var broken = new BrokenFileStore();
        await SetUpAsync(new ReportsFactory(runJobs: true, fileStore: broken));
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);

        var runId = await RequestAsync(Body());

        Assert.Equal(ReportJob.Attempts + 1, broken.Attempts); // the first try plus every retry
        var run = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{runId}");
        Assert.Equal("Failed", run.GetProperty("status").GetString());
        Assert.Contains("Blob storage unavailable", run.GetProperty("failureReason").GetString());
        var audits = await _factory.QueryAsync(db => db.AuditLogs.Where(a => a.EntityType == nameof(ReportRun) && a.EntityId == runId.ToString()).ToListAsync());
        // The failure transition is audited, attributed to the system actor (the job has no user).
        // (Status itself is serialized as an ordinal — U14 — so the reason text is what is asserted.)
        var failure = Assert.Single(audits, a => a.Action == "Update" && a.AfterSnapshot!.Contains("Blob storage unavailable"));
        Assert.Equal(Guid.Empty, failure.ActorUserId);
    }

    [Fact] // ADR-0007: the job runs with NO HttpContext and still sees only its own institution's data
    public async Task The_job_generates_from_its_own_institution_only_even_without_a_request_context()
    {
        var other = await _factory.SeedInstitutionAsync("Other School");
        var otherClass = await _factory.SeedClassAsync(other, "Grade 5", "A");
        var foreign = await _factory.SeedStudentAsync(other, otherClass, "SR-FOREIGN", "R-F");
        await _factory.SeedRecordAsync(other, foreign, Mon, AttendanceOutcome.Absent, Guid.NewGuid());
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);

        var runId = await RequestAsync(Body()); // whole school, this institution

        var run = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/reports/{runId}");
        Assert.Equal("Ready", run.GetProperty("status").GetString());
        var csv = await _client.GetStringAsync($"/api/v1/reports/{runId}/download");
        Assert.Contains("SR-1", csv);
        Assert.DoesNotContain("SR-FOREIGN", csv);
        var generated = await _factory.QueryAsync(db => db.AuditLogs.SingleAsync(a => a.EntityId == runId.ToString() && a.Action == "Update"));
        Assert.Equal(Guid.Empty, generated.ActorUserId); // proves the save happened outside the request context
    }
}
