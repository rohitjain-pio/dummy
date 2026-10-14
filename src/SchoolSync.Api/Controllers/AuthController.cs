using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolSync.Api.Errors;
using SchoolSync.Application.Auth;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthService authService, TimeProvider clock) : ControllerBase
{
    // CT-801 / U38 / API-006. `Role` and `InstitutionId` are NULLABLE and only ever honoured on
    // the self-service branch (VAL-007: Student or Parent). `StaffCode` selects the other branch —
    // and BRL-075's selector is the JSON property being OMITTED OR NULL, which maps exactly onto
    // `string?` here: omitted and `null` both bind to null, while `""` and `"   "` bind to
    // themselves and therefore count as PRESENT (1.0.5/m2).
    public record RegisterBody(string Username, string Email, string Password, string? Role, Guid? InstitutionId, string? StaffCode);
    public record LoginBody(string Username, string Password);

    // Q-232 (Rashmi Mehta, 2026-09-14) — NFR-017 / 09-security-spec.md §4 "token storage hardened
    // against script-readable persistence". The refresh token is issued ONLY as this cookie and is
    // never in a response body, so browser script cannot read or exfiltrate it:
    //  - HttpOnly: invisible to JavaScript;  Secure: HTTPS only;
    //  - SameSite=Strict: never sent on a cross-site request (the refresh endpoint is CSRF-safe);
    //  - Path=/api/v1/auth: sent only to the auth endpoints, not with every API call.
    // The 30-minute access token (CFG-002) is returned in the body and held in memory by the SPA.
    public const string RefreshCookieName = "ss_refresh";
    private const string RefreshCookiePath = "/api/v1/auth";
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(14); // CFG-002

    private void SetRefreshCookie(string refreshToken) =>
        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = clock.GetUtcNow().Add(RefreshLifetime),
            IsEssential = true
        });

    private void ClearRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = RefreshCookiePath });

    // API-006. Q-202 (provisional, operator-confirmed): PlatformOperator is excluded from the
    // set of roles this endpoint will accept, even if a caller names it directly — that role is
    // provisioned only through an internal path, never self-registration.
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterBody body, CancellationToken ct)
    {
        // CT-801 / U38 / E4. This parse is now LENIENT on purpose, and the change matters: on the
        // code branch the body's `role` must be IGNORED, not rejected, because rejecting a
        // conflicting value would tell a prober which institution a code belongs to. So an
        // unparseable or undefined role becomes `null` here and the decision moves to
        // AuthService, which ignores it on the code branch and refuses it with ERR-901 on the
        // self-service branch (VAL-007's allowlist).
        //
        // The old version rejected anything but a letter-initial, defined, non-PlatformOperator
        // role right here — and then handed every OTHER role straight through to account
        // creation. That was the Critical.
        UserRole? role = null;
        if (!string.IsNullOrWhiteSpace(body.Role)
            && char.IsLetter(body.Role[0])
            && Enum.TryParse<UserRole>(body.Role, out var parsed)
            && Enum.IsDefined(parsed))
            role = parsed;

        var result = await authService.RegisterAsync(
            new RegisterRequest(
                body.Username, body.Email, body.Password, role, body.InstitutionId,
                body.StaffCode,
                // BRL-075's branch selector: present iff the property was supplied and not null.
                StaffCodePresent: body.StaffCode is not null), ct);

        if (!result.Succeeded)
            return result.Failure switch
            {
                AuthFailureReason.PasswordPolicyViolation => BadRequest(ErrCatalogue.Err201),
                AuthFailureReason.DuplicateUsernameOrEmail => Conflict(ErrCatalogue.Err203),
                // CT-801 / BRL-077. ERR-206 answers unknown, revoked, consumed, malformed and
                // empty codes identically — the endpoint must be neither a code-format oracle nor
                // a branch-selector oracle (VAL-027, N13). ERR-207 (expired) is the single
                // distinguishable failure, because a holder of a real code needs to be told to
                // ask for a new one.
                AuthFailureReason.CodeNotRedeemable => StatusCode(StatusCodes.Status410Gone, ErrCatalogue.Err206),
                AuthFailureReason.CodeExpired => StatusCode(StatusCodes.Status410Gone, ErrCatalogue.Err207),
                _ => BadRequest(ErrCatalogue.Err901)
            };

        SetRefreshCookie(result.Value!.RefreshToken);
        return StatusCode(StatusCodes.Status201Created, ToSessionBody(result.Value!));
    }

    // API-007.
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginBody body, CancellationToken ct)
    {
        var result = await authService.LoginAsync(new LoginRequest(body.Username, body.Password), ct);

        if (!result.Succeeded)
            return Unauthorized(ErrCatalogue.Err202); // ERR-202 — deliberately generic

        SetRefreshCookie(result.Value!.RefreshToken);
        return Ok(ToSessionBody(result.Value!));
    }

    // API-008. The refresh token comes from the cookie; the cookie is cleared whether or not it
    // was still valid, so a logged-out browser holds nothing.
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) && !string.IsNullOrEmpty(refreshToken))
            await authService.LogoutAsync(userId, refreshToken, ct);
        ClearRefreshCookie();
        return NoContent();
    }

    // API-009. Anonymous by design: it is how an SPA with only an in-memory access token restores a
    // session after a reload. The rotated refresh token replaces the cookie.
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) || string.IsNullOrEmpty(refreshToken))
            return Unauthorized(ErrCatalogue.Err204);

        var result = await authService.RefreshAsync(new RefreshRequest(refreshToken), ct);
        if (!result.Succeeded)
        {
            ClearRefreshCookie();
            return Unauthorized(ErrCatalogue.Err204);
        }

        SetRefreshCookie(result.Value!.RefreshToken);
        return Ok(ToSessionBody(result.Value!));
    }

    // API-010.
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        var institutionId = User.FindFirstValue("institution_id");
        var role = User.FindFirstValue(ClaimTypes.Role);
        return Ok(new { userId, institutionId, role });
    }

    // No refreshToken member — Q-232.
    private static object ToSessionBody(SessionResult session) => new
    {
        accessToken = session.AccessToken,
        user = new
        {
            id = session.User.Id,
            username = session.User.Username,
            role = session.User.Role.ToString(),
            institutionId = session.User.InstitutionId
        }
    };
}
