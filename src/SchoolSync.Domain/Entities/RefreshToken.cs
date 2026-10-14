namespace SchoolSync.Domain.Entities;

// The denylist store for BRL-016/API-008/API-009. Storage mechanism is a routine implementation
// choice not named by 07-business-logic/identity-access.md or 09-security-spec.md — a DB table
// keeps the denylist correct across multiple App Service instances (no in-memory/single-node
// assumption), consistent with the rest of this design's persistence choices.
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserAccountId { get; set; }
    public UserAccount UserAccount { get; set; } = null!;

    // Store a hash of the token, never the raw token value (same principle as PasswordHash /
    // Room.PasscodeHash elsewhere in this model).
    public required string TokenHash { get; set; }

    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    // Replay protection (09-security-spec.md §1.4): set the moment this token is exchanged for
    // a new pair via /auth/refresh. A rotated-and-already-used token can never be replayed.
    public DateTime? RotatedAtUtc { get; set; }

    // BRL-016: sign-out denylists immediately. A password change denylists every outstanding
    // token for the user (defense-in-depth), not just the current session's token.
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsUsable(DateTime utcNow) =>
        RotatedAtUtc is null && RevokedAtUtc is null && ExpiresAtUtc > utcNow;
}
