using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolSync.Domain.Entities;
using Xunit;

namespace SchoolSync.IntegrationTests.Institutions;

// 14-test-spec/institutions.md: TC-101 through TC-107.
public class InstitutionsEndpointsTests : IAsyncLifetime
{
    private InstitutionsApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new InstitutionsApiFactory();
        _client = _factory.CreateClient();
        await _factory.EnsureDatabaseSeededAsync();
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

    private static object ValidBody(string name, string email, string postalCode) => new
    {
        name,
        registrationNumber = $"REG-{Guid.NewGuid():N}",
        contactEmail = email,
        telephone = "+919000000000",
        postalAddress = "1 St",
        postalCode,
        city = "City",
        region = "Region"
    };

    private record InstitutionBody(Guid Id, string Name, bool IsActive, DateTime? DeactivatedAtUtc, Guid SubscriptionPlanId);
    private record ErrorBody(string Code, string Message);
    private record DirectoryEntryBody(Guid Id, string Name, string City, string Region);

    [Fact] // TC-101
    public async Task Valid_PlatformOperator_registers_an_institution_successfully()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.PlatformOperator, Guid.Empty);

        var response = await _client.PostAsJsonAsync("/api/v1/institutions",
            ValidBody("Green Valley School", "admin@greenvalley.edu", "560001"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InstitutionBody>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.Id);
    }

    [Fact] // TC-102 — adversarial: fails against a naive single-field-uniqueness implementation
    public async Task Institutions_matching_only_name_and_email_but_different_postal_code_are_not_duplicates()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.PlatformOperator, Guid.Empty);

        var first = await _client.PostAsJsonAsync("/api/v1/institutions",
            ValidBody("Shared Name School", "shared@example.edu", "111111"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/v1/institutions",
            ValidBody("Shared Name School", "shared@example.edu", "222222")); // different postal only

        Assert.Equal(HttpStatusCode.Created, second.StatusCode); // NOT a duplicate — BRL-003 requires all three
    }

    [Fact] // TC-103 — adversarial: fails against an implementation that only checks active institutions
    public async Task Registering_with_the_same_triple_as_a_withdrawn_institution_is_still_rejected_as_duplicate()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.PlatformOperator, Guid.Empty);

        var body = ValidBody("Old School", "old@example.edu", "333333");
        var created = await _client.PostAsJsonAsync("/api/v1/institutions", body);
        var createdBody = await created.Content.ReadFromJsonAsync<InstitutionBody>();

        var withdrawResponse = await _client.PostAsync($"/api/v1/institutions/{createdBody!.Id}/withdraw", null);
        Assert.Equal(HttpStatusCode.OK, withdrawResponse.StatusCode);

        var duplicate = await _client.PostAsJsonAsync("/api/v1/institutions", body); // exact same triple again

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var error = await duplicate.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-101", error!.Code);
    }

    [Fact] // TC-104 — adversarial: fails against an implementation that only checks role, not institution match
    public async Task Administrator_of_one_institution_cannot_update_a_different_institutions_details()
    {
        var institutionA = await _factory.SeedInstitutionAsync("Institution A");
        var institutionB = await _factory.SeedInstitutionAsync("Institution B");
        AuthenticateAs(Guid.NewGuid(), UserRole.Administrator, institutionA);

        var response = await _client.PutAsJsonAsync($"/api/v1/institutions/{institutionB}",
            ValidBody("Hijacked Name", "hijack@example.edu", "999999"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-102", error!.Code);
    }

    [Fact] // TC-105 — adversarial: fails if contactEmail/postalAddress leak into the response
    public async Task Public_directory_search_never_exposes_contact_email_or_postal_address()
    {
        var seededId = await _factory.SeedInstitutionAsync("Green Meadows School");

        var response = await _client.GetAsync("/api/v1/public/institutions?query=Green");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("contactEmail", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("postalAddress", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("registrationNumber", raw, StringComparison.OrdinalIgnoreCase);

        var results = await response.Content.ReadFromJsonAsync<List<DirectoryEntryBody>>();
        // Q-235: the entry carries the institution id the UI-005 register typeahead submits as institutionId
        Assert.Contains(results!, r => r.Name == "Green Meadows School" && r.Id == seededId);
    }

    [Fact] // TC-106
    public async Task Withdraw_then_reactivate_toggles_IsActive_and_never_deletes_the_row()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.PlatformOperator, Guid.Empty);
        var created = await _client.PostAsJsonAsync("/api/v1/institutions",
            ValidBody("Toggle School", "toggle@example.edu", "444444"));
        var body = await created.Content.ReadFromJsonAsync<InstitutionBody>();

        var withdrawn = await _client.PostAsync($"/api/v1/institutions/{body!.Id}/withdraw", null);
        var withdrawnBody = await withdrawn.Content.ReadFromJsonAsync<InstitutionBody>();
        Assert.False(withdrawnBody!.IsActive);
        Assert.NotNull(withdrawnBody.DeactivatedAtUtc);

        var reactivated = await _client.PostAsync($"/api/v1/institutions/{body.Id}/reactivate", null);
        var reactivatedBody = await reactivated.Content.ReadFromJsonAsync<InstitutionBody>();
        Assert.True(reactivatedBody!.IsActive);
        Assert.Null(reactivatedBody.DeactivatedAtUtc);
    }

    // U7 — API-001 enforces BRL-004 (VAL-001/003/004). Each row starts from a body that TC-101
    // proves is accepted and breaks exactly one field, so a pass cannot come from a blanket reject.
    [Theory]
    [InlineData("contactEmail", "not-an-email")] // VAL-001
    [InlineData("contactEmail", "")] // VAL-001: required
    [InlineData("telephone", "12345")] // VAL-003: 5 digits, below the provisional 7
    [InlineData("telephone", "+91 900000000x")] // VAL-003: a letter
    [InlineData("postalCode", "56000")] // VAL-004: 5 digits
    [InlineData("postalCode", "56000A")] // VAL-004: 6 characters, not all digits
    [InlineData("name", " ")] // ENT-001: required
    [InlineData("registrationNumber", "")] // ENT-001: required
    public async Task Registration_with_one_invalid_field_is_rejected_with_ERR_901_and_creates_nothing(string field, string value)
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.PlatformOperator, Guid.Empty);
        var name = $"Invalid {field} School {Guid.NewGuid():N}";
        var body = new Dictionary<string, string>
        {
            ["name"] = name,
            ["registrationNumber"] = $"REG-{Guid.NewGuid():N}",
            ["contactEmail"] = "valid@example.edu",
            ["telephone"] = "+919000000000",
            ["postalAddress"] = "1 St",
            ["postalCode"] = "560001",
            ["city"] = "City",
            ["region"] = "Region"
        };
        body[field] = value;

        var response = await _client.PostAsJsonAsync("/api/v1/institutions", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-901", error!.Code);

        // Nothing persisted: the rejected institution never reaches the public directory.
        if (field != "name")
        {
            var directory = await _client.GetFromJsonAsync<List<DirectoryEntryBody>>(
                $"/api/v1/public/institutions?query={Uri.EscapeDataString(name)}");
            Assert.DoesNotContain(directory!, r => r.Name == name);
        }
    }

    [Theory] // U7 positive controls at the VAL-003/VAL-004 boundaries: these must still be accepted
    [InlineData("1234567", "000000")] // exactly 7 digits; all-zero 6-digit postal code
    [InlineData("+1 (234) 567-8901", "999999")] // separators tolerated
    public async Task Registration_at_the_validation_boundaries_is_accepted(string telephone, string postalCode)
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.PlatformOperator, Guid.Empty);
        var body = new Dictionary<string, string>
        {
            ["name"] = $"Boundary School {Guid.NewGuid():N}",
            ["registrationNumber"] = $"REG-{Guid.NewGuid():N}",
            ["contactEmail"] = "boundary@example.edu",
            ["telephone"] = telephone,
            ["postalAddress"] = "1 St",
            ["postalCode"] = postalCode,
            ["city"] = "City",
            ["region"] = "Region"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/institutions", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact] // TC-107 — adversarial: fails against an implementation that leaves subscription_plan_id null
    public async Task Registration_auto_assigns_the_Entry_plan_with_no_separate_subscription_call()
    {
        AuthenticateAs(Guid.NewGuid(), UserRole.PlatformOperator, Guid.Empty);

        var response = await _client.PostAsJsonAsync("/api/v1/institutions",
            ValidBody("Instant Capacity School", "instant@example.edu", "555555"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<InstitutionBody>();

        // The adversarial assertion: never Guid.Empty/null, and specifically the Entry plan's
        // well-known id — never a placeholder that merely looks non-null.
        Assert.NotEqual(Guid.Empty, body!.SubscriptionPlanId);
        Assert.Equal(SchoolSync.Infrastructure.Persistence.SchoolSyncDbContext.EntryPlanId, body.SubscriptionPlanId);
        // The "capacity-consuming action succeeds immediately" half of TC-107 (e.g. enrolling a
        // student) is exercised together with CT-102's own tests once student enrollment exists
        // — Student CRUD is not built yet at this point in the phase.
    }
}
