using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hangfire;
using Hangfire.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolSync.Api.Jobs;
using SchoolSync.Application.Commercial;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Commercial;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Attendance;
using Xunit;

namespace SchoolSync.IntegrationTests.Commercial;

// CT-504 — API-063 + the reversion job, PF-013. Gate: TC-704, TC-705. DoD: no billing call exists on
// the auto-reversion path.
public class TrialLifecycleTests : IAsyncLifetime
{
    private AttendanceApiFactory _factory = null!; // settable clock: 2026-09-14 06:00 UTC
    private HttpClient _client = null!;
    private Guid _institutionId, _classId, _operatorId;

    public async Task InitializeAsync()
    {
        _factory = new AttendanceApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        _classId = await _factory.SeedClassAsync(_institutionId);
        _operatorId = await _factory.SeedAccountAsync(_institutionId, UserRole.PlatformOperator);
        await _factory.QueryAsync(async db =>
        {
            (await db.SubscriptionPlans.SingleAsync(p => p.Id == SchoolSyncDbContext.EntryPlanId)).LearnerCapacity = 1; // Entry: 1 learner
            return await db.SaveChangesAsync();
        });
        await _factory.SeedStudentAsync(_institutionId, _classId, "SR-1", "R-1"); // Entry is now full
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role, Guid? institutionId = null) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, institutionId ?? _institutionId));

    private Task<HttpResponseMessage> StartTrialAsync(Guid? institutionId = null) =>
        _client.PostAsJsonAsync("/api/v1/trials", new { institutionId = institutionId ?? _institutionId, cardNumber = "4111111111111111" });

    private async Task<HttpStatusCode> EnrolAsync(string sr) => (await _client.PostAsJsonAsync("/api/v1/students", new
    {
        srNumber = sr,
        rollNumber = "R-" + sr,
        name = "Learner",
        dateOfBirth = "2016-04-02",
        gender = "Female",
        email = $"{sr}@example.edu",
        classId = _classId
    })).StatusCode;

    private Task<Institution> InstitutionAsync() => _factory.QueryAsync(db => db.Institutions.AsNoTracking().SingleAsync(i => i.Id == _institutionId));

    private async Task<int> RunReversionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TrialReversionJob>().RunAsync(CancellationToken.None);
    }

    [Fact] // FR-070 / Q-230: 30-day Mid trial, and the trial plan's entitlements genuinely apply
    public async Task Starting_a_trial_grants_the_mid_plan_for_30_days()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        Assert.Equal(HttpStatusCode.PaymentRequired, await EnrolAsync("SR-2")); // Entry full

        var response = await StartTrialAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new DateTime(2026, 10, 14, 6, 0, 0, DateTimeKind.Utc), body.GetProperty("trialExpiresAtUtc").GetDateTime().ToUniversalTime()); // CFG-006
        Assert.Equal(SchoolSyncDbContext.MidPlanId, body.GetProperty("trialPlanId").GetGuid());
        Assert.Equal(HttpStatusCode.Created, await EnrolAsync("SR-2")); // Mid capacity applies during the trial
        Assert.Equal(SchoolSyncDbContext.EntryPlanId, (await InstitutionAsync()).SubscriptionPlanId); // the SUBSCRIPTION itself is unchanged
    }

    [Fact] // TC-704 — purchase before day 30: subscription set, trial closed, the reversion job has no effect
    public async Task Buying_a_plan_during_the_trial_closes_it_and_the_reversion_job_leaves_it_alone()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        await StartTrialAsync();
        _factory.Clock.Advance(TimeSpan.FromDays(10));

        AuthenticateAs(_operatorId, UserRole.PlatformOperator, Guid.Empty);
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync("/api/v1/subscriptions", new { institutionId = _institutionId, subscriptionPlanId = SchoolSyncDbContext.BespokePlanId, startDate = "2026-09-24" })).StatusCode);

        var afterPurchase = await InstitutionAsync();
        Assert.Equal(SchoolSyncDbContext.BespokePlanId, afterPurchase.SubscriptionPlanId);
        Assert.Null(afterPurchase.TrialPlanId);
        Assert.NotNull(afterPurchase.TrialStartedAtUtc); // the one trial is used

        _factory.Clock.Advance(TimeSpan.FromDays(25)); // well past the original expiry
        Assert.Equal(0, await RunReversionAsync());
        Assert.Equal(SchoolSyncDbContext.BespokePlanId, (await InstitutionAsync()).SubscriptionPlanId);
    }

    [Fact] // TC-705 — no purchase: day 30 passes, the plan reverts to Entry, and the reversion is audited
    public async Task An_unpurchased_trial_reverts_to_entry_when_it_expires()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        await StartTrialAsync();

        _factory.Clock.Advance(TimeSpan.FromDays(30) - TimeSpan.FromSeconds(1));
        Assert.Equal(0, await RunReversionAsync()); // not yet

        _factory.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, await RunReversionAsync());

        var reverted = await InstitutionAsync();
        Assert.Equal(SchoolSyncDbContext.EntryPlanId, reverted.SubscriptionPlanId);
        Assert.Null(reverted.TrialPlanId);
        Assert.Null(reverted.TrialExpiresAtUtc);
        Assert.Contains(await _factory.QueryAsync(db => db.AuditLogs.Where(a => a.EntityType == nameof(Institution) && a.EntityId == _institutionId.ToString()).ToListAsync()),
            a => a.Action == "Update" && a.ActorUserId == Guid.Empty); // the system reverted it
    }

    [Fact] // TC-705 — adversarial, structural: nothing on the reversion path can bill, charge or take payment
    public void No_billing_or_payment_dependency_exists_on_the_trial_path_or_anywhere_in_the_product()
    {
        var billing = new Regex(@"billing|payment|charge|invoice|stripe|razorpay|paypal|card", RegexOptions.IgnoreCase);

        foreach (var type in new[] { typeof(TrialReversionJob), typeof(TrialService) })
            foreach (var parameter in type.GetConstructors().SelectMany(c => c.GetParameters()))
                Assert.DoesNotMatch(billing, parameter.ParameterType.FullName!);

        var productTypes = new[] { typeof(TrialService).Assembly, typeof(TrialReversionJob).Assembly, typeof(Institution).Assembly, typeof(TrialReversionJob).Assembly }
            .Distinct().SelectMany(a => a.GetTypes()).Select(t => t.FullName!).Where(n => !n.Contains('<'));
        Assert.DoesNotContain(productTypes, n => Regex.IsMatch(n.Split('.').Last(), @"Billing|Payment|Charge|Invoice"));

        var root = FindRepoRoot();
        foreach (var relative in new[] { "src/SchoolSync.Infrastructure/Commercial/TrialReversionJob.cs", "src/SchoolSync.Application/Commercial/TrialService.cs" })
        {
            var code = string.Join('\n', File.ReadAllLines(Path.Combine(root, relative)).Where(l => !l.TrimStart().StartsWith("//")))
                .Replace("SchoolInCharge", ""); // the role name BR-007 defines, not a charge — the one known false positive
            Assert.DoesNotMatch(billing, code); // comments may explain the rule; code may not implement one
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SchoolSync.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    [Fact] // BRL-071 is not only the nightly job: an expired trial stops granting entitlements immediately
    public async Task Entitlements_fall_back_to_the_subscription_the_moment_the_trial_expires()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        await StartTrialAsync();
        _factory.Clock.Advance(TimeSpan.FromDays(31)); // the job has NOT run

        Assert.Equal(HttpStatusCode.PaymentRequired, await EnrolAsync("SR-2")); // back to Entry's capacity of 1
    }

    [Fact] // Q-230: once per institution, and only from Entry
    public async Task A_trial_can_be_started_only_once_and_only_from_the_entry_plan()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        Assert.Equal(HttpStatusCode.Created, (await StartTrialAsync()).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await StartTrialAsync()).StatusCode);

        var paid = await _factory.SeedInstitutionAsync("Paid School");
        await _factory.SetPlanAsync(paid, SchoolSyncDbContext.BespokePlanId);
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal, paid);
        Assert.Equal(HttpStatusCode.Conflict, (await StartTrialAsync(paid)).StatusCode);
    }

    [Fact] // Q-230 — adversarial: never anonymous, never for another institution; a real PlatformOperator may
    public async Task Only_the_institutions_own_leadership_or_a_platform_operator_can_start_its_trial()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await StartTrialAsync()).StatusCode);

        var other = await _factory.SeedInstitutionAsync("Other School");
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal, other);
        Assert.Equal(HttpStatusCode.Forbidden, (await StartTrialAsync(_institutionId)).StatusCode); // someone else's school

        AuthenticateAs(Guid.NewGuid(), UserRole.Teacher);
        Assert.Equal(HttpStatusCode.Forbidden, (await StartTrialAsync()).StatusCode);
        Assert.Null((await InstitutionAsync()).TrialStartedAtUtc);

        AuthenticateAs(_operatorId, UserRole.PlatformOperator, Guid.Empty);
        Assert.Equal(HttpStatusCode.Created, (await StartTrialAsync()).StatusCode);
    }

    [Fact] // A4: the reversion runs daily on the same recurring scheduler as the rollover
    public void The_trial_reversion_job_is_scheduled_daily()
    {
        var manager = new RecordingManager();
        RecurringJobs.Register(manager);
        Assert.Contains(manager.Added, j => j.Id == TrialReversionJob.Id && j.Type == typeof(TrialReversionJob) && j.Cron == Cron.Daily());
        Assert.Contains(manager.Added, j => j.Id == SchoolSync.Infrastructure.Configuration.YearEndRolloverJob.Id);
    }

    [Fact] // The plan ids the Application layer must know match the seeded rows
    public void Trial_plan_constants_match_the_seeded_plans()
    {
        Assert.Equal(SchoolSyncDbContext.MidPlanId, TrialService.TrialPlanId);
        Assert.Equal(SchoolSyncDbContext.EntryPlanId, TrialService.EntryPlanId);
    }

    private sealed class RecordingManager : IRecurringJobManager
    {
        public readonly List<(string Id, Type Type, string Cron)> Added = [];
        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options) => Added.Add((recurringJobId, job.Type, cronExpression));
        public void Trigger(string recurringJobId) { }
        public void RemoveIfExists(string recurringJobId) { }
    }
}
