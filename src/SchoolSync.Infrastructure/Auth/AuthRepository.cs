using Microsoft.EntityFrameworkCore;
using SchoolSync.Application.Auth;
using SchoolSync.Domain.Entities;
using SchoolSync.Infrastructure.Persistence;

namespace SchoolSync.Infrastructure.Auth;

public class AuthRepository(SchoolSyncDbContext db) : IAuthRepository
{
    public Task<bool> UsernameOrEmailExistsAsync(string username, string email, CancellationToken ct) =>
        // Deliberately BypassTenantFilter-agnostic: UserAccount IS tenant-owned, but username/
        // email uniqueness is platform-global (VAL-006) — the caller (registration, anonymous)
        // has no institution scope of its own yet, so this must see across every institution.
        // Achieved via IgnoreQueryFilters, the one explicitly-marked escape hatch this query
        // needs — not a general bypass of the tenant filter for this repository.
        db.UserAccounts.IgnoreQueryFilters()
            .AnyAsync(u => u.Username == username || u.Email == email, ct);

    public async Task AddUserAsync(UserAccount user, CancellationToken ct) =>
        await db.UserAccounts.AddAsync(user, ct);

    public Task<UserAccount?> FindByUsernameAsync(string username, CancellationToken ct) =>
        db.UserAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<UserAccount?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.UserAccounts.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct) =>
        await db.RefreshTokens.AddAsync(token, ct);

    public Task<RefreshToken?> FindRefreshTokenByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task RevokeAllRefreshTokensForUserAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tokens = await db.RefreshTokens
            .Where(t => t.UserAccountId == userId && t.RevokedAtUtc == null && t.RotatedAtUtc == null)
            .ToListAsync(ct);
        foreach (var t in tokens) t.RevokedAtUtc = now;
    }

    // CT-801 / U38. Redemption is anonymous — there is no tenant on the request yet, and the
    // code's own row is what SUPPLIES the institution — so this read necessarily bypasses the
    // global tenant filter. It is the same shape as ADR-0004's parent-link-code exception and is
    // safe for the same reason: the lookup key is a keyed HMAC of a secret the caller had to
    // possess, so an unknown code resolves to nothing and no tenant data is reachable through it.
    // Recorded as ADR-0010.
    public Task<LeadershipCode?> FindRedeemableCodeByLookupAsync(string codeLookup, CancellationToken ct) =>
        db.LeadershipCodes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.CodeLookup == codeLookup && c.Status == LeadershipCodeStatus.Issued, ct);

    // BRL-077 + §2.12a's concurrency rule. The UPDATE is CONDITIONAL on the row still being
    // Issued, so of two concurrent redemptions exactly one can win — the loser writes nothing and
    // is told ERR-206. An unconditional "load, mutate, save" would let both through and create two
    // accounts from one code, which is the same check-then-act shape as U29.
    public async Task<bool> TryConsumeCodeAsync(Guid codeId, Guid consumedByUserId, DateTime consumedAtUtc, CancellationToken ct)
    {
        var code = await db.LeadershipCodes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == codeId && c.Status == LeadershipCodeStatus.Issued, ct);
        if (code is null) return false;

        code.Status = LeadershipCodeStatus.Consumed;
        code.ConsumedAtUtc = consumedAtUtc;
        code.ConsumedByUserId = consumedByUserId;

        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // The RowVersion token caught a competing redemption between the read and the write.
            // Losing this race is a normal outcome, not an error: the caller is told ERR-206,
            // exactly as if the code had already been consumed — which it has.
            db.Entry(code).State = EntityState.Detached;
            return false;
        }
    }

    // E5: a code issued before the institution was withdrawn must not create new staff accounts.
    public Task<bool> InstitutionIsActiveAsync(Guid institutionId, CancellationToken ct) =>
        db.Institutions.IgnoreQueryFilters().AnyAsync(i => i.Id == institutionId && i.IsActive, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
