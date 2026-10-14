using SchoolSync.Domain.Entities;

namespace SchoolSync.Application.Auth;

// Kept deliberately narrow — exactly what AuthService needs, so Application stays free of any
// EF Core/Infrastructure dependency (Clean/Onion, D13).
public interface IAuthRepository
{
    Task<bool> UsernameOrEmailExistsAsync(string username, string email, CancellationToken ct);
    Task AddUserAsync(UserAccount user, CancellationToken ct);
    Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken ct);
    Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken ct);

    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct);
    Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken ct);
    Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken ct);

    // CT-801 / U38. The redemption path. Looked up by the keyed code_lookup, never by plaintext.
    Task<LeadershipCode?> FindRedeemableCodeByLookupAsync(string codeLookup, CancellationToken ct);

    // BRL-077 + 04-data-model.md 2.12a concurrency rule: one code creates exactly ONE account.
    // Conditional on the row still being Issued, so two concurrent redemptions cannot both win.
    Task<bool> TryConsumeCodeAsync(Guid codeId, Guid consumedByUserId, DateTime consumedAtUtc, CancellationToken ct);

    // E5: a code issued before the institution was withdrawn must not create new staff accounts.
    Task<bool> InstitutionIsActiveAsync(Guid institutionId, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
