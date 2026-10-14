using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolSync.Domain.Entities;
using SchoolSync.IntegrationTests.Common;
using Xunit;

namespace SchoolSync.IntegrationTests.Students;

// 14-test-spec/academic-records.md TC-301 (Layer: Integration), plus the BR-021 guardian-field
// access gate that CT-102's Definition of Done names explicitly.
public class StudentsEndpointsTests : IAsyncLifetime
{
    private SchoolSyncApiFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _institutionId;
    private Guid _classId;

    public async Task InitializeAsync()
    {
        _factory = new SchoolSyncApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
        _institutionId = await _factory.SeedInstitutionAsync();
        _classId = await _factory.SeedClassAsync(_institutionId);
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private void AuthenticateAs(UserRole role) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.IssueTokenFor(Guid.NewGuid(), role, _institutionId));

    private object ValidBody(string srNumber = "SR-0001", string rollNumber = "ROLL-0001") => new
    {
        srNumber,
        rollNumber,
        name = "Asha Menon",
        dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-10).ToString("yyyy-MM-dd"),
        gender = "Female",
        email = "asha.menon@example.edu",
        classId = _classId,
        guardianFatherName = "Ravi Menon",
        guardianMotherName = "Lata Menon",
        guardianContactEmail = "ravi.menon@example.edu",
        guardianPhone = "+919000000001"
    };

    private record StudentBody(Guid Id, string SrNumber, string RollNumber, string Name, Guid? ClassId,
        string? GuardianFatherName, string? GuardianContactEmail, string? GuardianPhone, bool IsActive);

    private record ErrorBody(string Code, string Message);

    [Fact] // TC-301
    public async Task Valid_payload_with_an_existing_class_enrols_the_student()
    {
        AuthenticateAs(UserRole.Administrator);

        var response = await _client.PostAsJsonAsync("/api/v1/students", ValidBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StudentBody>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
        Assert.Equal(_classId, body.ClassId);
        Assert.True(body.IsActive);
    }

    [Fact] // TC-301 companion at the transport layer — BRL-018 surfaces as ERR-301, not a 500
    public async Task Duplicate_SR_number_returns_ERR_301()
    {
        AuthenticateAs(UserRole.Administrator);
        var first = await _client.PostAsJsonAsync("/api/v1/students", ValidBody("SR-0001", "ROLL-0001"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Same SR number, different roll number — the adversarial shape, checked here end-to-end.
        var second = await _client.PostAsJsonAsync("/api/v1/students", ValidBody("SR-0001", "ROLL-0002"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var error = await second.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-301", error!.Code);
    }

    [Fact] // DoD: "guardian fields access-gated per BR-021" — no anonymous path reaches them
    public async Task Guardian_fields_are_unreachable_without_authentication()
    {
        AuthenticateAs(UserRole.Administrator);
        var created = await _client.PostAsJsonAsync("/api/v1/students", ValidBody());
        var student = await created.Content.ReadFromJsonAsync<StudentBody>();

        _client.DefaultRequestHeaders.Authorization = null;
        var anonymous = await _client.GetAsync($"/api/v1/students/{student!.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        // Asserted on the raw body, not a typed shape: a typed read would silently tolerate a
        // guardian field leaking through in a response that merely failed to bind.
        var raw = await anonymous.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Ravi Menon", raw);
        Assert.DoesNotContain("ravi.menon@example.edu", raw);
    }

    [Fact] // BRL-021: guardian data rides the SAME access rule as the record — no looser path,
           // and equally no STRICTER one that would hide it from a reader entitled to the record
    public async Task An_authorised_reader_receives_the_guardian_fields_with_the_record()
    {
        AuthenticateAs(UserRole.Administrator);
        var created = await _client.PostAsJsonAsync("/api/v1/students", ValidBody());
        var student = await created.Content.ReadFromJsonAsync<StudentBody>();

        var response = await _client.GetAsync($"/api/v1/students/{student!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StudentBody>();
        Assert.Equal("Ravi Menon", body!.GuardianFatherName);
        Assert.Equal("ravi.menon@example.edu", body.GuardianContactEmail);
    }

    [Fact] // API-014: "leadership + Teacher (read)"
    public async Task Teacher_can_read_a_student_record()
    {
        AuthenticateAs(UserRole.Administrator);
        var created = await _client.PostAsJsonAsync("/api/v1/students", ValidBody());
        var student = await created.Content.ReadFromJsonAsync<StudentBody>();

        AuthenticateAs(UserRole.Teacher);
        var response = await _client.GetAsync($"/api/v1/students/{student!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact] // API-011 is leadership-only; BRL-029's read-only Teacher posture applied to students
    public async Task Teacher_cannot_enrol_a_student()
    {
        AuthenticateAs(UserRole.Teacher);

        var response = await _client.PostAsJsonAsync("/api/v1/students", ValidBody());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact] // BRL-020 + the tenant filter: another institution's student is simply not there
    public async Task A_student_of_another_institution_is_not_readable()
    {
        var otherInstitutionId = await _factory.SeedInstitutionAsync("Other School");
        var otherClassId = await _factory.SeedClassAsync(otherInstitutionId);
        var otherStudentId = await _factory.SeedStudentAsync(otherInstitutionId, otherClassId, "SR-9999", "ROLL-9999");

        AuthenticateAs(UserRole.Administrator); // authenticated against _institutionId

        var response = await _client.GetAsync($"/api/v1/students/{otherStudentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact] // API-014's roster read
    public async Task Class_roster_returns_the_students_placed_in_that_class()
    {
        AuthenticateAs(UserRole.Administrator);
        await _client.PostAsJsonAsync("/api/v1/students", ValidBody("SR-0001", "ROLL-0001"));
        await _client.PostAsJsonAsync("/api/v1/students", ValidBody("SR-0002", "ROLL-0002"));

        var response = await _client.GetAsync($"/api/v1/classes/{_classId}/roster");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var roster = await response.Content.ReadFromJsonAsync<List<StudentBody>>();
        Assert.Equal(2, roster!.Count);
    }

    [Fact] // API-013 — SPEC-FR-014, reversible withdrawal
    public async Task Withdrawing_a_student_deactivates_without_deleting()
    {
        AuthenticateAs(UserRole.Administrator);
        var created = await _client.PostAsJsonAsync("/api/v1/students", ValidBody());
        var student = await created.Content.ReadFromJsonAsync<StudentBody>();

        var withdraw = await _client.PostAsync($"/api/v1/students/{student!.Id}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);

        // Still retrievable — withdrawal is a state change, not a removal (D8/BR-005).
        var afterWithdrawal = await _client.GetAsync($"/api/v1/students/{student.Id}");
        Assert.Equal(HttpStatusCode.OK, afterWithdrawal.StatusCode);
        var body = await afterWithdrawal.Content.ReadFromJsonAsync<StudentBody>();
        Assert.False(body!.IsActive);
    }

    [Fact] // API-015 — SPEC-FR-016
    public async Task Linking_a_student_to_a_user_account_succeeds()
    {
        AuthenticateAs(UserRole.Administrator);
        var created = await _client.PostAsJsonAsync("/api/v1/students", ValidBody());
        var student = await created.Content.ReadFromJsonAsync<StudentBody>();
        var accountId = await _factory.SeedUserAccountAsync(_institutionId, UserRole.Student);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/students/{student!.Id}/link-account", new { userAccountId = accountId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
