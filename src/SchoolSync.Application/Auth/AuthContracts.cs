using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Auth;

// CT-801 / U38 / BRL-075. Role and InstitutionId are NULLABLE and are only ever honoured on the
// self-service branch, where VAL-007 restricts Role to {Student, Parent}. On the code branch they
// are ignored outright. StaffCodePresent is carried separately from StaffCode because BRL-075s
// branch selector is "the JSON property was omitted or null", which a plain null string cannot
// express once "" and "   " must count as PRESENT (1.0.5/m2).
public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    UserRole? Role,
    Guid? InstitutionId,
    string? StaffCode,
    bool StaffCodePresent);
public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);

public record UserSummary(Guid Id, string Username, UserRole Role, Guid InstitutionId);
public record SessionResult(string AccessToken, string RefreshToken, UserSummary User);
public record CurrentIdentity(Guid UserId, Guid InstitutionId, UserRole Role);

// Every failure mode this service can produce, mapped 1:1 to a catalogue ERR- code by the
// controller (05-api-contracts/identity-access.md's Errors rows) — never a raw exception leaking
// past the Application boundary, and never a generic message the controller has to guess at.
public enum AuthFailureReason
{
    None,
    PasswordPolicyViolation, // ERR-201
    DuplicateUsernameOrEmail, // ERR-203
    InvalidCredentials, // ERR-202 — deliberately generic, never distinguishes unknown-user from wrong-password
    SessionExpiredOrRevoked, // ERR-204
    // CT-801 / U38 / BRL-077. ERR-206 is deliberately the answer to unknown, revoked, consumed,
    // malformed AND empty codes alike - byte-identical, so the endpoint is neither a code-format
    // oracle nor a branch-selector oracle (VAL-027, N13).
    CodeNotRedeemable, // ERR-206
    CodeExpired, // ERR-207
    RoleNotSelfSelectable // ERR-901 - VAL-007: only Student and Parent may be named in the body
}

public record AuthResult<T>(bool Succeeded, T? Value, AuthFailureReason Failure = AuthFailureReason.None)
{
    public static AuthResult<T> Ok(T value) => new(true, value);
    public static AuthResult<T> Fail(AuthFailureReason reason) => new(false, default, reason);
}

public interface IAuthService
{
    Task<AuthResult<SessionResult>> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResult<SessionResult>> LoginAsync(LoginRequest request, CancellationToken ct);
    Task LogoutAsync(Guid userId, string presentedRefreshToken, CancellationToken ct);
    Task<AuthResult<SessionResult>> RefreshAsync(RefreshRequest request, CancellationToken ct);
}

public interface IPasswordPolicy
{
    // VAL-005 / BRL-014 / CFG-001: min 10 chars, >=3 of {upper, lower, digit, symbol}, not on
    // the blocklist. Returns true only when every condition holds.
    bool IsSatisfiedBy(string password);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    // CFG-002: 30-min access token. Claims: sub (user id), role, institution_id (09-security-spec.md §1).
    string IssueAccessToken(UserAccount user);

    // Returns the raw refresh token (given to the caller) — only its hash is ever persisted.
    string IssueRefreshTokenValue();
    string HashRefreshToken(string rawToken);
}
