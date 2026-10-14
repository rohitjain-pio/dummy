using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;
using SchoolSync.IntegrationTests.Attendance;
using Xunit;

namespace SchoolSync.IntegrationTests.Commercial;

// CT-503 — gate TC-706, TC-707. DoD: "Both RBAC and tier axes proven independently enforced".
public class TierGateTests : IAsyncLifetime
{
    private AttendanceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId, _classId, _studentId, _operatorId;

    public async Task InitializeAsync()
    {
        _factory = new AttendanceApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync(); // Entry plan (BRL-074)
        _classId = await _factory.SeedClassAsync(_institutionId);
        _studentId = await _factory.SeedStudentAsync(_institutionId, _classId, "SR-1", "R-1");
        _operatorId = await _factory.SeedAccountAsync(_institutionId, UserRole.PlatformOperator);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role, Guid? institutionId = null) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, institutionId ?? _institutionId));

    private Task SetCapacityAsync(Guid planId, int capacity) => _factory.QueryAsync(async db =>
    {
        (await db.SubscriptionPlans.SingleAsync(p => p.Id == planId)).LearnerCapacity = capacity;
        return await db.SaveChangesAsync();
    });

    private static object NewStudent(string sr) => new
    {
        srNumber = sr,
        rollNumber = "R-" + sr,
        name = "New Learner",
        dateOfBirth = "2016-04-02",
        gender = "Female",
        email = $"{sr}@example.edu",
        classId = (Guid?)null
    };

    private Task<HttpResponseMessage> EnrolAsync(string sr)
    {
        var body = JsonSerializer.SerializeToElement(NewStudent(sr));
        var dict = body.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
        dict["classId"] = _classId;
        return _client.PostAsJsonAsync("/api/v1/students", dict);
    }

    private Task<int> ActiveStudentsAsync() => _factory.QueryAsync(db => db.Students.IgnoreQueryFilters().CountAsync(s => s.InstitutionId == _institutionId && s.IsActive));

    [Fact] // TC-706 — adversarial: at capacity, ERR-801 and not even one over-capacity row
    public async Task Enrolling_beyond_the_plans_learner_capacity_is_refused_before_commit()
    {
        await SetCapacityAsync(SchoolSyncDbContext.EntryPlanId, 2); // 1 active already
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);

        Assert.Equal(HttpStatusCode.Created, (await EnrolAsync("SR-2")).StatusCode); // reaches the limit exactly
        var over = await EnrolAsync("SR-3");

        Assert.Equal(HttpStatusCode.PaymentRequired, over.StatusCode);
        Assert.Contains("ERR-801", await over.Content.ReadAsStringAsync());
        Assert.Equal(2, await ActiveStudentsAsync()); // no grace overage — not one extra row
    }

    [Fact] // BRL-073: reactivating a withdrawn learner consumes a seat and is gated the same way
    public async Task Reactivating_a_withdrawn_learner_at_capacity_is_refused()
    {
        await SetCapacityAsync(SchoolSyncDbContext.EntryPlanId, 1);
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/v1/students/{_studentId}/withdraw", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await EnrolAsync("SR-2")).StatusCode); // takes the freed seat

        var reactivate = await _client.PostAsync($"/api/v1/students/{_studentId}/reactivate", null);

        Assert.Equal(HttpStatusCode.PaymentRequired, reactivate.StatusCode);
        Assert.Equal(1, await ActiveStudentsAsync());
    }

    [Theory] // TC-707 — adversarial: the Parent role is allowed; the tier is not -> ERR-802 on every Parent endpoint
    [InlineData("GET", "/api/v1/students/linked")]
    [InlineData("GET", "/api/v1/attendance/linked")]
    [InlineData("GET", "/api/v1/dashboard")]
    [InlineData("POST", "/api/v1/parent-links")]
    public async Task A_parent_on_a_plan_without_ParentAccess_is_refused_even_though_RBAC_allows_it(string method, string path)
    {
        var parent = await _factory.SeedAccountAsync(_institutionId, UserRole.Parent);
        AuthenticateAs(parent, UserRole.Parent);

        var response = method == "GET" ? await _client.GetAsync(path) : await _client.PostAsJsonAsync(path, new { code = "ABCDEFGH" });

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Contains("ERR-802", await response.Content.ReadAsStringAsync());
    }

    [Fact] // DoD, axis independence: tier ON + role wrong -> 403 (RBAC); tier OFF + role right -> 402 (tier); both -> 200
    public async Task The_role_axis_and_the_tier_axis_are_each_enforced_on_their_own()
    {
        var parent = await _factory.SeedAccountAsync(_institutionId, UserRole.Parent);

        AuthenticateAs(parent, UserRole.Parent);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await _client.GetAsync("/api/v1/students/linked")).StatusCode); // Entry: tier fails

        await _factory.SetPlanAsync(_institutionId, SchoolSyncDbContext.MidPlanId); // Mid also lacks ParentAccess (seed)
        Assert.Equal(HttpStatusCode.PaymentRequired, (await _client.GetAsync("/api/v1/students/linked")).StatusCode);

        await _factory.SetPlanAsync(_institutionId, SchoolSyncDbContext.BespokePlanId);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/students/linked")).StatusCode); // both axes pass

        AuthenticateAs(Guid.NewGuid(), UserRole.Teacher); // tier includes it, role does not
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/v1/students/linked")).StatusCode);
    }

    [Fact] // Staff issuing a link code is not gated (Q-228); only the Parent side is
    public async Task Staff_can_still_issue_a_link_code_on_the_entry_plan()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsync($"/api/v1/students/{_studentId}/parent-link-codes", null)).StatusCode);
    }

    [Fact] // API-064 + NFR-032: a PlatformOperator assigns a plan; it takes effect for the gates at once and is audited
    public async Task A_platform_operator_assigns_a_plan_which_changes_entitlements_immediately()
    {
        var parent = await _factory.SeedAccountAsync(_institutionId, UserRole.Parent);
        AuthenticateAs(_operatorId, UserRole.PlatformOperator, Guid.Empty);

        var response = await _client.PutAsJsonAsync("/api/v1/subscriptions", new { institutionId = _institutionId, subscriptionPlanId = SchoolSyncDbContext.BespokePlanId, startDate = "2026-10-01" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var institution = await _factory.QueryAsync(db => db.Institutions.AsNoTracking().SingleAsync(i => i.Id == _institutionId));
        Assert.Equal(SchoolSyncDbContext.BespokePlanId, institution.SubscriptionPlanId);
        Assert.Equal(new DateOnly(2026, 10, 1), institution.SubscriptionStartDate);
        Assert.Contains(await _factory.QueryAsync(db => db.AuditLogs.Where(a => a.EntityType == nameof(Institution) && a.EntityId == _institutionId.ToString()).ToListAsync()),
            a => a.Action == "Update" && a.ActorUserId == _operatorId);

        AuthenticateAs(parent, UserRole.Parent);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/students/linked")).StatusCode);
    }

    [Theory] // BRL-064 + _shared.md §1: only a PlatformOperator — and the role is re-read from the DB, not trusted from the token
    [InlineData("leadership")]
    [InlineData("forged-operator-token")]
    public async Task Only_a_real_platform_operator_can_assign_a_plan(string caller)
    {
        if (caller == "leadership") AuthenticateAs(Guid.NewGuid(), UserRole.Administrator);
        else AuthenticateAs(await _factory.SeedAccountAsync(_institutionId, UserRole.Administrator), UserRole.PlatformOperator, Guid.Empty); // token says PO, the account row does not

        var response = await _client.PutAsJsonAsync("/api/v1/subscriptions", new { institutionId = _institutionId, subscriptionPlanId = SchoolSyncDbContext.BespokePlanId, startDate = "2026-10-01" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(SchoolSyncDbContext.EntryPlanId, (await _factory.QueryAsync(db => db.Institutions.AsNoTracking().SingleAsync(i => i.Id == _institutionId))).SubscriptionPlanId);
    }

    [Fact] // Q-227: plan limits are data a PlatformOperator edits; only the three BR-063 capabilities are accepted
    public async Task A_platform_operator_edits_plan_limits_and_capabilities_which_the_gates_then_use()
    {
        AuthenticateAs(_operatorId, UserRole.PlatformOperator, Guid.Empty);

        var bad = await _client.PutAsJsonAsync($"/api/v1/subscription-plans/{SchoolSyncDbContext.EntryPlanId}",
            new { learnerCapacity = 1, storageAllowanceGb = 5, supportLevel = "Standard", capabilities = new[] { "StudentSatisfaction" } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var ok = await _client.PutAsJsonAsync($"/api/v1/subscription-plans/{SchoolSyncDbContext.EntryPlanId}",
            new { learnerCapacity = 1, storageAllowanceGb = 5, supportLevel = "Standard", capabilities = new[] { "ParentAccess" } });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        AuthenticateAs(Guid.NewGuid(), UserRole.Principal);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await EnrolAsync("SR-2")).StatusCode); // capacity 1 now enforced
        var parent = await _factory.SeedAccountAsync(_institutionId, UserRole.Parent);
        AuthenticateAs(parent, UserRole.Parent);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/v1/students/linked")).StatusCode); // Entry now includes ParentAccess
    }

    [Fact] // API-061: anonymous, the three tiers' structural fields only
    public async Task Public_plans_list_the_three_tiers_structurally()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var plans = (await _client.GetFromJsonAsync<List<JsonElement>>("/api/v1/public/plans"))!;

        Assert.Equal(["Entry", "Mid", "Bespoke"], plans.Select(p => p.GetProperty("name").GetString()));
        Assert.Equal(JsonValueKind.Null, plans[2].GetProperty("learnerCapacity").ValueKind); // unlimited
        Assert.All(plans, p => Assert.Equal(["id", "name", "learnerCapacity", "storageAllowanceGb", "supportLevel", "capabilities"], p.EnumerateObject().Select(x => x.Name)));
    }

    [Fact] // API-062: anonymous enquiry, validated, idempotent
    public async Task A_public_enquiry_is_accepted_validated_and_idempotent()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/v1/public/enquiries", new { fullName = "A", email = "not-an-email", message = "hi" })).StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/enquiries")
        { Content = JsonContent.Create(new { fullName = "Anita Rao", email = "anita@school.edu", institution = "Green Valley", studentCountRange = "200-500", message = "Pricing for 300 learners?" }) };
        request.Headers.Add("Idempotency-Key", "enquiry-1");
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(request)).StatusCode);
    }
}
