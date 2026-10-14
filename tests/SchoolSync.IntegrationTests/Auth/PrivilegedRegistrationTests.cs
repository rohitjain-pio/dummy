using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolSync.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace SchoolSync.IntegrationTests.Auth;

/// <summary>
/// CT-801 — TC-209, the regression test for REV-001 / U38 (Critical).
///
/// The defect: <c>POST /auth/register</c> was anonymous and took <c>role</c> and
/// <c>institutionId</c> from the request body, excluding only PlatformOperator, and
/// <c>AuthService.RegisterAsync</c> issued a live session without any approval or verification.
/// The public directory publishes institution ids, so two unauthenticated calls produced a token
/// satisfying <c>[Authorize(Roles = Roles.Leadership)]</c> across the whole product.
///
/// Written adversarially, per 14-test-spec §TC-209:
/// - assertions are on the RAW JSON, never a deserialised DTO that could silently drop a field;
/// - it asserts the ABSENCE OF ACCESS TO PII, not merely a status code, because a refactor that
///   returns 400 while still writing the account would pass a status-only assertion;
/// - it covers the numeric-enum class, which is where the historical hole in this codebase
///   actually was — VAL-007's allowlist is safe by construction, but "safe by construction" is an
///   argument, not a test.
///
/// **This test must remain in the suite permanently. Deleting or weakening it is a class-6 review
/// finding.** It was observed RED against the pre-fix implementation before the fix was written.
/// </summary>
public class PrivilegedRegistrationTests(ITestOutputHelper output) : IAsyncLifetime
{
    private AuthApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new AuthApiFactory();
        _client = _factory.CreateClient();
        await _factory.SeedBaselineInstitutionAsync();

        // The public directory (API-005) reads a PROJECTION table refreshed on institution
        // create/update/withdraw (Tech Design A5) — not the Institution table directly. The
        // baseline seeder writes the Institution only, so without this the directory is empty and
        // every assertion below would fail for a fixture reason rather than the real one. Found by
        // running this test red and reading WHY it was red, which is the whole point of doing so.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        db.PublicInstitutionDirectory.Add(new SchoolSync.Domain.Entities.PublicInstitutionDirectoryProjection
        {
            InstitutionId = _factory.TestInstitutionId,
            Name = "Test Institution",
            City = "Test City",
            Region = "Test Region"
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>The five privileged roles, by canonical name (TC-209 step 3).</summary>
    public static TheoryData<string> PrivilegedRoleNames() =>
        ["Administrator", "Principal", "SchoolInCharge", "Teacher", "PlatformOperator"];

    /// <summary>
    /// TC-209 step 3b — the numeric-enum class and case variants. This is the case the original
    /// hole actually was: a numeric string binding to an enum. "99" is an UNDEFINED value and must
    /// not produce an account either.
    /// </summary>
    public static TheoryData<string> NonAllowlistedRoleLiterals() =>
        ["\"Administrator\"", "\"Principal\"", "\"SchoolInCharge\"", "\"Teacher\"", "\"PlatformOperator\"",
         "2", "99", "\"99\"", "\"0\"", "\"administrator\"", "\"ADMINISTRATOR\""];

    [Theory]
    [MemberData(nameof(NonAllowlistedRoleLiterals))]
    public async Task TC209_anonymous_registration_naming_a_privileged_role_creates_no_account_and_grants_no_access(string roleLiteral)
    {
        // (0) The institution id is obtained exactly as REV-001 obtained it — from the anonymous
        //     public directory. This is the two-call sequence, not a fixture shortcut.
        var institutionId = await InstitutionIdFromPublicDirectoryAsync();

        var username = "attacker-" + Guid.NewGuid().ToString("N")[..8];
        var json = $$"""
        {
          "username": "{{username}}",
          "email": "{{username}}@x.test",
          "password": "Correct-Horse9",
          "role": {{roleLiteral}},
          "institutionId": "{{institutionId}}"
        }
        """;

        var response = await _client.PostAsync("/api/v1/auth/register",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var raw = await response.Content.ReadAsStringAsync();
        output.WriteLine($"role={roleLiteral} -> {(int)response.StatusCode} {response.StatusCode}");

        // (1) No session material, asserted on the RAW JSON.
        Assert.DoesNotContain("accessToken", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // (2) No UserAccount row exists for that username.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
            var exists = await db.UserAccounts.IgnoreQueryFilters().AnyAsync(u => u.Username == username);
            Assert.False(exists, $"a UserAccount row was created for role literal {roleLiteral}");
        }

        // (4) The real proof: whatever token the response yielded (none is expected — if one was
        //     returned the test has already failed above), it must not reach learner PII.
        var token = TryExtractToken(raw);
        var probe = _factory.CreateClient();
        if (token is not null)
            probe.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var students = await probe.GetAsync("/api/v1/students");
        Assert.True(students.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"expected 401/403 reading learner records, got {(int)students.StatusCode}");
        var studentsRaw = await students.Content.ReadAsStringAsync();
        Assert.DoesNotContain("srNumber", studentsRaw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// TC-209 step 3c — an empty or whitespace <c>staffCode</c> selects the CODE branch and fails
    /// there (ERR-206). It must never fall through to a self-service registration, and must never
    /// produce an account. BRL-075's 1.0.5 branch selector: absent IFF omitted or null.
    /// </summary>
    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    public async Task TC209c_empty_or_whitespace_staffCode_selects_the_code_branch_and_creates_no_account(string staffCodeLiteral)
    {
        var institutionId = await InstitutionIdFromPublicDirectoryAsync();
        var username = "attacker-" + Guid.NewGuid().ToString("N")[..8];

        var json = $$"""
        {
          "username": "{{username}}",
          "email": "{{username}}@x.test",
          "password": "Correct-Horse9",
          "role": "Administrator",
          "institutionId": "{{institutionId}}",
          "staffCode": {{staffCodeLiteral}}
        }
        """;

        var response = await _client.PostAsync("/api/v1/auth/register",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var raw = await response.Content.ReadAsStringAsync();
        output.WriteLine($"staffCode={staffCodeLiteral} -> {(int)response.StatusCode}: {raw}");

        Assert.DoesNotContain("accessToken", raw, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ERR-206", raw, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolSyncDbContext>();
        Assert.False(await db.UserAccounts.IgnoreQueryFilters().AnyAsync(u => u.Username == username));
    }

    /// <summary>
    /// The positive control, so the test above cannot pass by the endpoint being broken outright.
    /// TC-202 branch A: a Student or Parent self-registration still succeeds immediately, with no
    /// pending state — FR-006's no-approval-step property survives the U38 fix.
    /// </summary>
    [Theory]
    [InlineData("Student")]
    [InlineData("Parent")]
    public async Task TC202a_self_service_registration_still_succeeds_for_the_allowlisted_roles(string role)
    {
        var institutionId = await InstitutionIdFromPublicDirectoryAsync();
        var username = "selfserve-" + Guid.NewGuid().ToString("N")[..8];

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username,
            email = username + "@x.test",
            password = "Correct-Horse9",
            role,
            institutionId
        });

        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("accessToken", raw, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>API-005, anonymous. The id is what mode A submits (U44, added at 1.0.5).</summary>
    private async Task<Guid> InstitutionIdFromPublicDirectoryAsync()
    {
        var response = await _client.GetAsync("/api/v1/public/institutions");
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        var first = doc.RootElement.EnumerateArray().FirstOrDefault();
        Assert.True(first.ValueKind == JsonValueKind.Object,
            "the public directory returned no institution — TC-209 needs the real two-call sequence");
        Assert.True(first.TryGetProperty("id", out var id),
            "API-005 must publish the institution id that UI-005 mode A submits (U44)");
        return id.GetGuid();
    }

    private static string? TryExtractToken(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("accessToken", out var t)
                && t.ValueKind == JsonValueKind.String)
                return t.GetString();
        }
        catch (JsonException) { /* an error body is not a session */ }
        return null;
    }
}
