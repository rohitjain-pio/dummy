using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolSync.Domain.Entities;
using SchoolSync.IntegrationTests.Common;
using Xunit;

namespace SchoolSync.IntegrationTests.ParentLinks;

// TC-308 (Integration) and TC-309 (E2E) from 14-test-spec/academic-records.md.
public class ParentLinkEndpointsTests : IAsyncLifetime
{
    private SchoolSyncApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionA;
    private Guid _classA;

    public async Task InitializeAsync()
    {
        _factory = new SchoolSyncApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionA = await _factory.SeedInstitutionAsync("Institution A");
        // CT-503 / Q-228: Parent endpoints are tier-gated on ParentAccess; these P1 tests exercise the
        // link state machine, not the tier, so the institution is on a plan that includes it.
        await _factory.SetPlanAsync(_institutionA, SchoolSync.Infrastructure.Persistence.SchoolSyncDbContext.BespokePlanId);
        _classA = await _factory.SeedClassAsync(_institutionA);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(Guid userId, UserRole role, Guid institutionId) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(userId, role, institutionId));

    private object StudentBody(string sr, string roll, Guid classId) => new
    {
        srNumber = sr,
        rollNumber = roll,
        name = "Asha Menon",
        dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10).ToString("yyyy-MM-dd"),
        gender = "Female",
        email = "asha.menon@example.edu",
        classId,
        guardianFatherName = (string?)null,
        guardianMotherName = (string?)null,
        guardianContactEmail = (string?)null,
        guardianPhone = (string?)null
    };

    private record CreatedStudent(Guid Id);
    private record IssuedCode(Guid CodeId, string Code, DateTime ExpiresAtUtc);
    private record LinkResult(Guid StudentId, DateTime LinkedAtUtc);
    private record LinkedStudentBody(Guid Id, string Name, string RollNumber, Guid? ClassId, bool IsActive);
    private record ErrorBody(string Code, string Message);

    // Enrols a student in institution A and issues a link code against them, as A's leadership.
    private async Task<(Guid StudentId, string Code)> EnrolAndIssueAsync(string sr = "SR-A1", string roll = "ROLL-A1")
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Administrator, _institutionA);

        var created = await _client.PostAsJsonAsync("/api/v1/students", StudentBody(sr, roll, _classA));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var student = await created.Content.ReadFromJsonAsync<CreatedStudent>();

        var issued = await _client.PostAsync($"/api/v1/students/{student!.Id}/parent-link-codes", null);
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        var code = await issued.Content.ReadFromJsonAsync<IssuedCode>();

        return (student.Id, code!.Code);
    }

    [Fact] // TC-308 — adversarial: fails against an implementation that skips the institution check
    public async Task A_code_issued_by_institution_A_is_rejected_for_a_parent_of_institution_B()
    {
        var (_, code) = await EnrolAndIssueAsync();

        var institutionB = await _factory.SeedInstitutionAsync("Institution B");
        await _factory.SetPlanAsync(institutionB, SchoolSync.Infrastructure.Persistence.SchoolSyncDbContext.BespokePlanId); // CT-503
        var parentOfB = await _factory.SeedUserAccountAsync(institutionB, UserRole.Parent);
        AuthenticateAs(parentOfB, UserRole.Parent, institutionB);

        var response = await _client.PostAsJsonAsync("/api/v1/parent-links", new { code });

        // ERR-309 is 403 per 08-error-catalogue.md — and specifically NOT ERR-310, which is what
        // a tenant-filtered lookup would have been forced to return (see ADR-0004).
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-309", error!.Code);
    }

    [Fact] // TC-309 — the full PF-005 flow, ending at the Parent's own scoped view
    public async Task Staff_issue_a_code_a_parent_redeems_it_and_the_child_becomes_visible()
    {
        var (studentId, code) = await EnrolAndIssueAsync();

        var parentUserId = await _factory.SeedUserAccountAsync(_institutionA, UserRole.Parent);
        AuthenticateAs(parentUserId, UserRole.Parent, _institutionA);

        // Before redeeming, the parent sees nothing.
        var before = await _client.GetAsync("/api/v1/students/linked");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Empty((await before.Content.ReadFromJsonAsync<List<LinkedStudentBody>>())!);

        var redeem = await _client.PostAsJsonAsync("/api/v1/parent-links", new { code });
        Assert.Equal(HttpStatusCode.OK, redeem.StatusCode);
        var link = await redeem.Content.ReadFromJsonAsync<LinkResult>();
        Assert.Equal(studentId, link!.StudentId);

        // After redeeming, the linked child is visible on the Parent's scoped endpoint (FR-075).
        var after = await _client.GetAsync("/api/v1/students/linked");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        var linked = await after.Content.ReadFromJsonAsync<List<LinkedStudentBody>>();
        var child = Assert.Single(linked!);
        Assert.Equal(studentId, child.Id);
        Assert.Equal("ROLL-A1", child.RollNumber);
    }

    [Fact] // BR-066: a Parent sees linked children ONLY — never another learner in the same school
    public async Task A_parent_does_not_see_an_unlinked_student_of_the_same_institution()
    {
        var (linkedStudentId, code) = await EnrolAndIssueAsync("SR-A1", "ROLL-A1");

        // A second student in the SAME institution, deliberately never linked to this parent.
        AuthenticateAs(Guid.NewGuid(), UserRole.Administrator, _institutionA);
        var otherCreated = await _client.PostAsJsonAsync("/api/v1/students", StudentBody("SR-A2", "ROLL-A2", _classA));
        Assert.Equal(HttpStatusCode.Created, otherCreated.StatusCode);

        var parentUserId = await _factory.SeedUserAccountAsync(_institutionA, UserRole.Parent);
        AuthenticateAs(parentUserId, UserRole.Parent, _institutionA);
        await _client.PostAsJsonAsync("/api/v1/parent-links", new { code });

        var linked = await (await _client.GetAsync("/api/v1/students/linked"))
            .Content.ReadFromJsonAsync<List<LinkedStudentBody>>();

        var child = Assert.Single(linked!); // exactly one, not both
        Assert.Equal(linkedStudentId, child.Id);
    }

    [Fact] // API-016 is leadership-only — a Parent cannot mint their own link code
    public async Task A_parent_cannot_issue_a_link_code()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Administrator, _institutionA);
        var created = await _client.PostAsJsonAsync("/api/v1/students", StudentBody("SR-A1", "ROLL-A1", _classA));
        var student = await created.Content.ReadFromJsonAsync<CreatedStudent>();

        var parentUserId = await _factory.SeedUserAccountAsync(_institutionA, UserRole.Parent);
        AuthenticateAs(parentUserId, UserRole.Parent, _institutionA);

        var response = await _client.PostAsync($"/api/v1/students/{student!.Id}/parent-link-codes", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] // API-018 is Parent-only — leadership cannot redeem a code on a parent's behalf
    public async Task Leadership_cannot_redeem_a_link_code()
    {
        var (_, code) = await EnrolAndIssueAsync(); // still authenticated as A's Administrator

        var response = await _client.PostAsJsonAsync("/api/v1/parent-links", new { code });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] // The revoke endpoint is reachable and the revoked code is then dead end-to-end
    public async Task A_revoked_code_is_rejected_when_redeemed()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Administrator, _institutionA);
        var created = await _client.PostAsJsonAsync("/api/v1/students", StudentBody("SR-A1", "ROLL-A1", _classA));
        var student = await created.Content.ReadFromJsonAsync<CreatedStudent>();
        var issued = await _client.PostAsync($"/api/v1/students/{student!.Id}/parent-link-codes", null);
        var code = await issued.Content.ReadFromJsonAsync<IssuedCode>();

        var revoke = await _client.PostAsync(
            $"/api/v1/students/{student.Id}/parent-link-codes/{code!.CodeId}/revoke", null);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);

        var parentUserId = await _factory.SeedUserAccountAsync(_institutionA, UserRole.Parent);
        AuthenticateAs(parentUserId, UserRole.Parent, _institutionA);
        var redeem = await _client.PostAsJsonAsync("/api/v1/parent-links", new { code = code.Code });

        Assert.Equal(HttpStatusCode.Gone, redeem.StatusCode); // ERR-310 is 410
        var error = await redeem.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-310", error!.Code);
    }

    [Fact] // API-016's response carries the 7-day expiry (CFG-005) the caller needs to relay
    public async Task An_issued_code_reports_its_expiry_seven_days_out()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.Administrator, _institutionA);
        var created = await _client.PostAsJsonAsync("/api/v1/students", StudentBody("SR-A1", "ROLL-A1", _classA));
        var student = await created.Content.ReadFromJsonAsync<CreatedStudent>();

        var issued = await _client.PostAsync($"/api/v1/students/{student!.Id}/parent-link-codes", null);
        var code = await issued.Content.ReadFromJsonAsync<IssuedCode>();

        var daysOut = (code!.ExpiresAtUtc - DateTime.UtcNow).TotalDays;
        Assert.InRange(daysOut, 6.9, 7.1);
    }
}
