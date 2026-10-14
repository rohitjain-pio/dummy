using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SchoolSync.IntegrationTests.Auth;

// CT-002 gate: "Register/login/logout/refresh work end-to-end against CT-001's schema" — this
// class is that proof, through the real HTTP pipeline (routing, JWT bearer, [Authorize]), not a
// unit test of AuthService in isolation.
public class AuthEndpointsTests : IAsyncLifetime
{
    private AuthApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new AuthApiFactory();
        await _factory.SeedBaselineInstitutionAsync();
        // Q-232: the refresh token travels only as a Secure cookie, so the client must speak https
        // and keep a cookie jar — exactly what a browser does.
        _client = CookieClient(handleCookies: true);
    }

    private HttpClient CookieClient(bool handleCookies) =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = handleCookies });

    private static string RefreshCookieFrom(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("ss_refresh="));
        return header["ss_refresh=".Length..header.IndexOf(';')];
    }

    private async Task<HttpStatusCode> RefreshWithExplicitCookieAsync(string token)
    {
        using var bare = CookieClient(handleCookies: false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"ss_refresh={token}");
        return (await bare.SendAsync(request)).StatusCode;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private record SessionBody(string AccessToken, UserBody User);
    private record UserBody(Guid Id, string Username, string Role, Guid InstitutionId);
    private record ErrorBody(string Code, string Message);

    [Fact]
    public async Task Full_register_login_refresh_logout_cycle_succeeds_end_to_end()
    {
        // Register
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "jane.doe",
            email = "jane@example.edu",
            password = "Correct-Horse9",
            // CT-801 / VAL-007: Teacher is no longer self-selectable. This test is about session
            // mechanics (register/login/refresh/logout), not about which role is used.
            role = "Student",
            institutionId = _factory.TestInstitutionId
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<SessionBody>();
        Assert.NotNull(registered);
        Assert.Equal("Student", registered!.User.Role);

        // Login (separately, proving the credential round-trip, not just the register response)
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "jane.doe", password = "Correct-Horse9" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = await loginResponse.Content.ReadFromJsonAsync<SessionBody>();
        Assert.NotNull(session);

        // /me while authenticated with the issued access token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var meResponse = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // Refresh — the client sends only the cookie. It rotates the token; the OLD one must stop working (replay protection)
        var originalToken = RefreshCookieFrom(loginResponse);
        var refreshResponse = await _client.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<SessionBody>();
        Assert.NotNull(rotated);
        var rotatedToken = RefreshCookieFrom(refreshResponse);
        Assert.NotEqual(originalToken, rotatedToken);

        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshWithExplicitCookieAsync(originalToken)); // ERR-204

        // Logout (the jar carries the CURRENT token), then prove it too stops working and the cookie is cleared
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", rotated!.AccessToken);
        var logoutResponse = await _client.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Contains(logoutResponse.Headers.GetValues("Set-Cookie"), c => c.StartsWith("ss_refresh=;"));

        Assert.Equal(HttpStatusCode.Unauthorized, await RefreshWithExplicitCookieAsync(rotatedToken)); // BRL-016: denylisted immediately
    }

    [Fact] // Q-232 / NFR-017 — adversarial: no auth response body carries the refresh token; the cookie is locked down
    public async Task The_refresh_token_is_only_ever_an_httpOnly_secure_strict_cookie_never_in_a_body()
    {
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "cookie.user",
            email = "cookie@example.edu",
            password = "Correct-Horse9",
            // CT-801 / VAL-007: Teacher is no longer self-selectable. This test is about session
            // mechanics (register/login/refresh/logout), not about which role is used.
            role = "Student",
            institutionId = _factory.TestInstitutionId
        });
        var login = await _client.PostAsJsonAsync("/api/v1/auth/login", new { username = "cookie.user", password = "Correct-Horse9" });
        var refresh = await _client.PostAsync("/api/v1/auth/refresh", null);

        foreach (var response in new[] { register, login, refresh })
        {
            Assert.True(response.IsSuccessStatusCode);
            var token = RefreshCookieFrom(response);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("refreshToken", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(token, body);

            var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("ss_refresh=")).ToLowerInvariant();
            Assert.Contains("httponly", cookie);
            Assert.Contains("secure", cookie);
            Assert.Contains("samesite=strict", cookie);
            Assert.Contains("path=/api/v1/auth", cookie);
        }
    }

    [Fact] // Q-232: a token posted in a JSON body is no longer honoured — only the cookie is
    public async Task Refresh_without_the_cookie_is_unauthorized_even_with_a_valid_token_in_the_body()
    {
        var register = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "body.token.user",
            email = "body@example.edu",
            password = "Correct-Horse9",
            // CT-801 / VAL-007: Teacher is no longer self-selectable. This test is about session
            // mechanics (register/login/refresh/logout), not about which role is used.
            role = "Student",
            institutionId = _factory.TestInstitutionId
        });
        var token = RefreshCookieFrom(register);

        using var bare = CookieClient(handleCookies: false);
        var response = await bare.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = token });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact] // CT-601: security headers on every response; SPA fallback never swallows /api 404s
    public async Task Responses_carry_the_CSP_and_unknown_api_routes_stay_404_not_html()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Registering_the_same_username_twice_is_rejected_as_a_conflict()
    {
        var body = new
        {
            username = "duplicate.user",
            email = "dup1@example.edu",
            password = "Correct-Horse9",
            role = "Student",
            institutionId = _factory.TestInstitutionId
        };
        var first = await _client.PostAsJsonAsync("/api/v1/auth/register", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/v1/auth/register",
            body with { }); // same username, different nothing — still a duplicate
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var error = await second.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-203", error!.Code);
    }

    [Fact]
    public async Task Registering_with_a_policy_violating_password_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "weak.pw.user",
            email = "weak@example.edu",
            password = "short",
            role = "Parent",
            institutionId = _factory.TestInstitutionId
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal("ERR-201", error!.Code);
    }

    [Fact]
    public async Task Login_with_wrong_password_and_login_with_unknown_username_return_the_identical_generic_error()
    {
        // Adversarial: proves the two failure modes are genuinely indistinguishable to the
        // caller, not just that both happen to be 401 — a policy that leaked "unknown user" vs
        // "wrong password" via a different message/code would still pass a weaker assertion.
        await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "real.user",
            email = "real@example.edu",
            password = "Correct-Horse9",
            role = "Student",
            institutionId = _factory.TestInstitutionId
        });

        var wrongPassword = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "real.user", password = "TotallyWrong9!" });
        var unknownUser = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { username = "does.not.exist", password = "Whatever9!" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);

        var wrongPasswordBody = await wrongPassword.Content.ReadFromJsonAsync<ErrorBody>();
        var unknownUserBody = await unknownUser.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.Equal(wrongPasswordBody, unknownUserBody);
    }

    [Fact]
    public async Task Registering_as_PlatformOperator_is_rejected_even_though_the_field_accepts_the_string()
    {
        // Q-202 (operator-confirmed, non-blocking): the public endpoint must reject this role
        // outright, not merely omit it from a UI dropdown — a direct API call is exactly the
        // case a UI-only restriction would miss.
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = "sneaky.operator",
            email = "sneaky@example.edu",
            password = "Correct-Horse9",
            role = "PlatformOperator",
            institutionId = _factory.TestInstitutionId
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory] // adversarial: numeric role strings parse as enums — "0" is PlatformOperator, "99" is no role at all
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("1")]
    [InlineData("teacher")]
    public async Task Registering_with_a_numeric_or_non_exact_role_string_is_rejected(string role)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            username = $"numeric.role.{role}",
            email = $"numeric{role}@example.edu",
            password = "Correct-Horse9",
            role,
            institutionId = _factory.TestInstitutionId
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Calling_me_without_a_bearer_token_is_unauthorized()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
