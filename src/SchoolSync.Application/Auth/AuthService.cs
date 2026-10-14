using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Auth;

// FR-006/FR-007/BRL-014/BRL-015/BRL-016/BRL-075/BRL-077/E1/E2/E3/E4/E5.
//
// CT-801 / U38. The prior version of this comment read: "enforced at the DTO/controller boundary,
// not here, since Role is a closed enum and this service trusts its caller already filtered the
// offered choices". THAT ASSUMPTION WAS THE CRITICAL. The caller here is an anonymous HTTP
// request; it filters nothing. REV-001 sent role=Administrator straight past the UI and this
// service issued a leadership session for any institution listed in the public directory.
//
// The rule now lives HERE, server-side, where the account is actually created: VAL-007 is an
// ALLOWLIST of {Student, Parent}, and a privileged role is reachable only by redeeming a code
// whose row supplies both the role and the institution. The request body is never a source of a
// privileged role, nor of an institution for one.
public class AuthService(
    IAuthRepository repository,
    IPasswordPolicy passwordPolicy,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    SchoolSync.Application.Common.ILeadershipCodeLookup codeLookup,
    TimeProvider clock) : IAuthService
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14); // CFG-002, D2
    private const int CodeLength = 8; // CFG-010

    // VAL-027: exact CFG-010 length, alphanumeric only, applied to the NORMALISED value. A
    // malformed code — wrong length (including zero), symbols, whitespace-only — is refused with
    // the same ERR-206 an unknown code gets, never a distinguishable "wrong format" (N13).
    private static bool IsWellFormedCode(string normalised) =>
        normalised.Length == CodeLength && normalised.All(char.IsAsciiLetterOrDigit);

    public async Task<AuthResult<SessionResult>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (!passwordPolicy.IsSatisfiedBy(request.Password))
            return AuthResult<SessionResult>.Fail(AuthFailureReason.PasswordPolicyViolation); // ERR-201

        // BRL-075: role and institution come from EXACTLY ONE of two sources, resolved before
        // anything is written. The branch selector is the PRESENCE of staffCode, not its validity
        // (1.0.5/m2) — a caller who sends the field, even empty, is asserting they hold a code and
        // is answered as such, never silently downgraded into self-service.
        UserRole role;
        Guid institutionId;
        LeadershipCode? redeemed = null;

        if (request.StaffCodePresent)
        {
            // Branch (b). Body role/institutionId are IGNORED, not rejected — rejecting the
            // conflict would tell a prober which institution a code belongs to (E4).
            var normalised = codeLookup.Normalise(request.StaffCode ?? string.Empty);
            if (!IsWellFormedCode(normalised))
                return AuthResult<SessionResult>.Fail(AuthFailureReason.CodeNotRedeemable); // ERR-206

            redeemed = await repository.FindRedeemableCodeByLookupAsync(codeLookup.ComputeLookup(normalised), ct);
            if (redeemed is null)
                return AuthResult<SessionResult>.Fail(AuthFailureReason.CodeNotRedeemable); // unknown/revoked/consumed

            var asOf = clock.GetUtcNow().UtcDateTime;
            if (redeemed.IsExpiredAt(asOf))
                return AuthResult<SessionResult>.Fail(AuthFailureReason.CodeExpired); // ERR-207 — the one distinguishable failure
            if (!redeemed.IsRedeemableAt(asOf))
                return AuthResult<SessionResult>.Fail(AuthFailureReason.CodeNotRedeemable); // ERR-206

            // E5: a withdrawn tenant must not gain new staff accounts through a code issued
            // before it was withdrawn.
            if (!await repository.InstitutionIsActiveAsync(redeemed.InstitutionId, ct))
                return AuthResult<SessionResult>.Fail(AuthFailureReason.CodeNotRedeemable); // ERR-206

            role = redeemed.GrantedRole;
            institutionId = redeemed.InstitutionId;
        }
        else
        {
            // Branch (a), self-service. VAL-007 is an ALLOWLIST — the three leadership roles,
            // Teacher, PlatformOperator and any undefined enum value that survived model binding
            // are all refused right here, not filtered by a caller we do not control.
            if (request.Role is not (UserRole.Student or UserRole.Parent))
                return AuthResult<SessionResult>.Fail(AuthFailureReason.RoleNotSelfSelectable); // ERR-901
            if (request.InstitutionId is not { } named || named == Guid.Empty)
                return AuthResult<SessionResult>.Fail(AuthFailureReason.RoleNotSelfSelectable); // ERR-901

            role = request.Role.Value;
            institutionId = named;
        }

        // VAL-006: platform-global uniqueness, not institution-scoped (E1). Deliberately checked
        // AFTER the branch resolves, so a caller probing roles cannot use it as a username oracle.
        if (await repository.UsernameOrEmailExistsAsync(request.Username, request.Email, ct))
            return AuthResult<SessionResult>.Fail(AuthFailureReason.DuplicateUsernameOrEmail); // ERR-203

        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            InstitutionId = institutionId, // BRL-008: immutable after insert
            Username = request.Username,
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = role,
            ProvisionedByCodeId = redeemed?.Id, // Q-241/U43: null means pre-1.0.5, not attributable
            IsActive = true
        };

        if (redeemed is not null)
        {
            // BRL-077 + §2.12a's concurrency rule: consume the code CONDITIONALLY on it still
            // being Issued, and create the account only if we won. Two concurrent redemptions of
            // one code must produce exactly one account, not two.
            if (!await repository.TryConsumeCodeAsync(redeemed.Id, user.Id, clock.GetUtcNow().UtcDateTime, ct))
                return AuthResult<SessionResult>.Fail(AuthFailureReason.CodeNotRedeemable); // lost the race
        }

        await repository.AddUserAsync(user, ct);
        await repository.SaveChangesAsync(ct);

        // FR-006: immediate session, no approval step — E2 applies regardless of any linked
        // Student/Teacher record existing yet. TC-202 asserts this survives on BOTH branches: for
        // a code redeemer, the code IS the prior approval.
        return AuthResult<SessionResult>.Ok(await IssueSessionAsync(user, ct));
    }

    public async Task<AuthResult<SessionResult>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await repository.FindByUsernameAsync(request.Username, ct);

        // ERR-202 is deliberately identical whether the username is unknown or the password is
        // wrong — never reveal which. Same for a deactivated (soft-deleted) account: it must not
        // be distinguishable from "wrong password" to an anonymous caller.
        if (user is null || !user.IsActive || !passwordHasher.Verify(request.Password, user.PasswordHash))
            return AuthResult<SessionResult>.Fail(AuthFailureReason.InvalidCredentials);

        return AuthResult<SessionResult>.Ok(await IssueSessionAsync(user, ct));
    }

    public async Task LogoutAsync(Guid userId, string presentedRefreshToken, CancellationToken ct)
    {
        // BRL-016: denylist immediately. Look the token up by its hash, exactly like refresh does.
        var hash = tokenService.HashRefreshToken(presentedRefreshToken);
        var token = await repository.FindRefreshTokenByHashAsync(hash, ct);
        if (token is not null && token.UserAccountId == userId)
        {
            token.RevokedAtUtc = clock.GetUtcNow().UtcDateTime;
            await repository.SaveChangesAsync(ct);
        }
        // Idempotent: logging out with an already-invalid token is still a 204, never an error —
        // the caller's intent (be signed out) is already satisfied.
    }

    public async Task<AuthResult<SessionResult>> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var existing = await repository.FindRefreshTokenByHashAsync(hash, ct);
        var now = clock.GetUtcNow().UtcDateTime;

        if (existing is null || !existing.IsUsable(now))
            return AuthResult<SessionResult>.Fail(AuthFailureReason.SessionExpiredOrRevoked); // ERR-204

        var user = await repository.FindByIdAsync(existing.UserAccountId, ct);
        if (user is null || !user.IsActive)
            return AuthResult<SessionResult>.Fail(AuthFailureReason.SessionExpiredOrRevoked);

        // Replay protection: mark the presented token rotated before issuing the new pair, so it
        // can never be exchanged again even if this request is somehow replayed concurrently.
        existing.RotatedAtUtc = now;
        var session = await IssueSessionAsync(user, ct);
        return AuthResult<SessionResult>.Ok(session);
    }

    private async Task<SessionResult> IssueSessionAsync(UserAccount user, CancellationToken ct)
    {
        var accessToken = tokenService.IssueAccessToken(user);
        var rawRefreshToken = tokenService.IssueRefreshTokenValue();
        var now = clock.GetUtcNow().UtcDateTime;

        await repository.AddRefreshTokenAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserAccountId = user.Id,
            TokenHash = tokenService.HashRefreshToken(rawRefreshToken),
            IssuedAtUtc = now,
            ExpiresAtUtc = now + RefreshTokenLifetime
        }, ct);
        await repository.SaveChangesAsync(ct);

        return new SessionResult(accessToken, rawRefreshToken,
            new UserSummary(user.Id, user.Username, user.Role, user.InstitutionId));
    }

    // BRL-016 defense-in-depth: a password change denylists every outstanding refresh token for
    // the user, not just the current session's. Exposed for the (not-yet-built) account-settings
    // password-change endpoint to call; CT-002's own scope has no password-change endpoint of its
    // own (that is FR-064's account-settings surface, CT-501), but the guard belongs here with the
    // rest of the session-lifecycle logic so it is never re-implemented differently elsewhere.
    public Task RevokeAllSessionsAsync(Guid userId, CancellationToken ct) =>
        repository.RevokeAllRefreshTokensForUserAsync(userId, ct);
}
