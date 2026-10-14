using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hangfire;
using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SchoolSync.Api.Jobs;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Configuration;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Attendance;
using SchoolSync.IntegrationTests.Fakes;
using Xunit;

namespace SchoolSync.IntegrationTests.Configuration;

// CT-501 — API-056–058. DoD: "Audit-on-save unconditional (BR-059)". Plus BRL-057/058, VAL-024/025.
public class InstitutionConfigEndpointsTests : IAsyncLifetime
{
    private AttendanceApiFactory _factory = null!; // clock: 2026-09-14 06:00 UTC
    private HttpClient _client = null!;
    private Guid _institutionId;
    private readonly Guid _principalId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _factory = new AttendanceApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role, Guid? institutionId = null) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, institutionId ?? _institutionId));

    private Task<HttpResponseMessage> PutGeneralAsync(string timeZone = "Asia/Kolkata", string language = "en", object? extra = null) =>
        _client.PutAsJsonAsync("/api/v1/institution-config", extra ?? new { academicYear = "2026-27", timeZone, language });

    [Fact] // BRL-059 — adversarial: the audit row is written on every save, and no request field can switch it off
    public async Task Every_configuration_save_is_audited_even_when_the_request_asks_it_not_to_be()
    {
        AuthenticateAs(_principalId, UserRole.Principal);

        Assert.Equal(HttpStatusCode.OK, (await PutGeneralAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutGeneralAsync(extra: new { academicYear = "2027-28", timeZone = "Asia/Kolkata", language = "en", audit = false, recordForAudit = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync("/api/v1/institution-config/notifications", new { email = true, sms = true, inPlatform = false })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync("/api/v1/institution-config/academic",
            new { gradingScale = "A-F", courseCategories = new[] { "Core", "Elective" }, assessmentTypes = new[] { "Unit test" }, reportCardFormat = "Termly" })).StatusCode);

        var audits = await _factory.QueryAsync(db => db.AuditLogs.Where(a => a.EntityType == nameof(InstitutionConfiguration) && a.InstitutionId == _institutionId).ToListAsync());
        Assert.Equal(4, audits.Count); // one per save — including the one that sent audit=false
        Assert.All(audits, a => Assert.Equal(_principalId, a.ActorUserId));
    }

    [Fact] // BRL-059 "effective immediately" + API-056 read for any authenticated user of the institution
    public async Task Saved_settings_are_immediately_visible_to_every_role()
    {
        AuthenticateAs(_principalId, UserRole.Principal);
        await PutGeneralAsync("America/New_York");
        await _client.PutAsJsonAsync("/api/v1/institution-config/academic", new { gradingScale = "Percent", courseCategories = new[] { "Core" }, assessmentTypes = Array.Empty<string>(), reportCardFormat = "Annual" });

        foreach (var role in new[] { UserRole.Teacher, UserRole.Student, UserRole.Parent })
        {
            AuthenticateAs(Guid.NewGuid(), role);
            var config = await _client.GetFromJsonAsync<JsonElement>("/api/v1/institution-config");
            Assert.Equal("America/New_York", config.GetProperty("timeZone").GetString());
            Assert.Equal("Percent", config.GetProperty("academic").GetProperty("gradingScale").GetString());
            Assert.False(config.GetProperty("isDefault").GetBoolean());
        }
    }

    [Fact] // BRL-059 effective immediately, proven through another feature: attendance's "today" follows the new zone
    public async Task A_configured_time_zone_changes_the_attendance_day_straight_away()
    {
        var classId = await _factory.SeedClassAsync(_institutionId);
        var student = await _factory.SeedStudentAsync(_institutionId, classId, "SR-1", "R-1");
        _factory.Clock.UtcNow = new DateTimeOffset(2026, 9, 14, 2, 0, 0, TimeSpan.Zero); // 07:30 IST on the 14th; 22:00 on the 13th in New York
        AuthenticateAs(_principalId, UserRole.Principal);

        async Task<HttpStatusCode> MarkForThe14thAsync() => (await _client.PutAsJsonAsync("/api/v1/attendance",
            new { classId, date = "2026-09-14", entries = new[] { new { personId = student, personType = "Student", outcome = "Present" } } })).StatusCode;

        Assert.Equal(HttpStatusCode.OK, await MarkForThe14thAsync()); // default zone: the 14th is today
        await PutGeneralAsync("America/New_York");
        Assert.Equal(HttpStatusCode.BadRequest, await MarkForThe14thAsync()); // in New York it is still the 13th: ERR-403 future date
    }

    [Theory] // BRL-058: only leadership changes configuration
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Parent)]
    public async Task Non_leadership_cannot_change_any_configuration(UserRole role)
    {
        AuthenticateAs(Guid.NewGuid(), role);

        Assert.Equal(HttpStatusCode.Forbidden, (await PutGeneralAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.PutAsJsonAsync("/api/v1/institution-config/notifications", new { email = true, sms = true, inPlatform = true })).StatusCode);
        Assert.Equal(0, await _factory.QueryAsync(db => db.InstitutionConfigurations.IgnoreQueryFilters().CountAsync()));
    }

    [Theory] // VAL-024 (IANA only — a Windows id is refused even on a Windows host) and VAL-025 (Q-209: English only)
    [InlineData("India Standard Time", "en")]
    [InlineData("Not/AZone", "en")]
    [InlineData("Asia/Kolkata", "fr")]
    public async Task Invalid_time_zones_and_unsupported_languages_are_rejected(string timeZone, string language)
    {
        AuthenticateAs(_principalId, UserRole.Principal);
        var response = await PutGeneralAsync(timeZone, language);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ERR-901", await response.Content.ReadAsStringAsync());
    }

    [Fact] // BRL-057: configuration applies only to its own institution
    public async Task One_institutions_configuration_never_reaches_another()
    {
        AuthenticateAs(_principalId, UserRole.Principal);
        await PutGeneralAsync("America/New_York");
        var other = await _factory.SeedInstitutionAsync("Other School");

        AuthenticateAs(Guid.NewGuid(), UserRole.Principal, other);
        var config = await _client.GetFromJsonAsync<JsonElement>("/api/v1/institution-config");

        Assert.True(config.GetProperty("isDefault").GetBoolean());
        Assert.Equal("Asia/Kolkata", config.GetProperty("timeZone").GetString()); // the CFG-009 fallback, not New York
    }
}

public class RecurringJobTests
{
    private sealed class RecordingManager : IRecurringJobManager
    {
        public readonly List<(string Id, Type Type, string Cron)> Added = [];
        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options) => Added.Add((recurringJobId, job.Type, cronExpression));
        public void Trigger(string recurringJobId) { }
        public void RemoveIfExists(string recurringJobId) { }
    }

    [Fact] // CT-501 scaffold + Q-216 closed: the lock sweep and retention sweep are scheduled daily
    public void The_daily_lock_sweep_and_retention_sweep_are_registered()
    {
        var manager = new RecordingManager();

        RecurringJobs.Register(manager);

        Assert.Contains(manager.Added, j => j.Id == RecurringJobIds.AttendanceLockSweep && j.Type == typeof(AttendanceLockSweepJob) && j.Cron == Cron.Daily());
        Assert.Contains(manager.Added, j => j.Id == RecurringJobIds.RetentionSweep && j.Type == typeof(RetentionSweepJob) && j.Cron == Cron.Daily());
    }

    [Fact] // _shared.md §4 scaffold: counts what is past CFG-007 across institutions, and changes NOTHING
    public async Task The_retention_sweep_scaffold_counts_expired_records_without_modifying_any()
    {
        var options = new DbContextOptionsBuilder<SchoolSyncDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new SchoolSyncDbContext(options, new FakeCurrentTenant()); // no tenant, like a job
        var clock = new TestClock(new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero));
        Student NewStudent(Guid institution, bool active, DateTime? deactivated) => new()
        {
            Id = Guid.NewGuid(),
            InstitutionId = institution,
            SrNumber = Guid.NewGuid().ToString(),
            RollNumber = Guid.NewGuid().ToString(),
            Name = "Former Student",
            DateOfBirth = new DateOnly(2000, 1, 1),
            Gender = "F",
            Email = "former@example.edu",
            IsActive = active,
            DeactivatedAtUtc = deactivated
        };
        db.Students.AddRange(
            NewStudent(Guid.NewGuid(), false, new DateTime(2019, 9, 1)), // past 7 years — institution 1
            NewStudent(Guid.NewGuid(), false, new DateTime(2018, 1, 1)), // past 7 years — institution 2
            NewStudent(Guid.NewGuid(), false, new DateTime(2020, 1, 1)), // not yet
            NewStudent(Guid.NewGuid(), true, null));
        await db.SaveChangesAsync();

        var (students, teachers, accounts) = await new RetentionSweepJob(db, clock, NullLogger<RetentionSweepJob>.Instance).RunAsync(CancellationToken.None);

        Assert.Equal(2, students);
        Assert.Equal(0, teachers);
        Assert.Equal(0, accounts);
        db.ChangeTracker.Clear();
        Assert.Equal(4, await db.Students.IgnoreQueryFilters().CountAsync());
        Assert.All(await db.Students.IgnoreQueryFilters().ToListAsync(), s => Assert.Equal("Former Student", s.Name)); // nothing anonymised
    }
}
