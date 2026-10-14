using SchoolSync.Domain.Common;

namespace SchoolSync.Domain.Entities;

/// <summary>
/// CT-801 / U38. The sign-up code that confers a privileged role. `04-data-model.md` §2.12a/§2.12b.
///
/// **The lifecycle is copied from <see cref="ParentLinkCode"/>; the storage is not.**
/// `ParentLinkCode.Code` is stored **plaintext**. This one stores a BCrypt <see cref="CodeHash"/>
/// and a keyed <see cref="CodeLookup"/>, and neither is inherited from anything already built or
/// tested — 1.0.4 described this as a copy of a tested mechanism, which under-sized exactly the
/// riskiest part of the task (U45d). The plaintext code is returned in exactly one response and is
/// never stored.
///
/// `status` is an explicit column here rather than derived from nullable timestamps the way
/// ParentLinkCode does it, because the filtered unique index on <see cref="CodeLookup"/> must be
/// filtered on it, and because `Expired` is derived at read time from <see cref="ExpiresAtUtc"/>
/// rather than stored — a stored expiry state drifts out of sync with the clock (§2.12a).
/// </summary>
public class LeadershipCode : ITenantOwned, IAuditable
{
    public Guid Id { get; set; }

    /// <summary>The institution the redeemed account will belong to. Read FROM here, never from the request body.</summary>
    public Guid InstitutionId { get; set; }
    public Institution Institution { get; set; } = null!;

    /// <summary>BCrypt of the normalised code. The plaintext is never stored (§2.12a).</summary>
    public required string CodeHash { get; set; }

    /// <summary>
    /// Base64url of HMAC-SHA256(CFG-SEC-06, normalise(code)) — 43 ASCII chars, never truncated
    /// (§2.12b(b)). Exists so a redemption is a single indexed read instead of a table scan of
    /// BCrypt comparisons. Unique while <see cref="Status"/> is Issued.
    /// </summary>
    public required string CodeLookup { get; set; }

    /// <summary>VAL-028: Administrator, Principal, SchoolInCharge or Teacher. Never PlatformOperator/Student/Parent.</summary>
    public UserRole GrantedRole { get; set; }

    public LeadershipCodeStatus Status { get; set; } = LeadershipCodeStatus.Issued;

    /// <summary>Null when issued by a PlatformOperator at institution create (API-001/API-065) — no account of this institution exists yet (§2.12a).</summary>
    public Guid? IssuedByUserId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; } // issued_at + CFG-010 (7 days)

    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? RevokedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = [];

    /// <summary>
    /// BRL-077: Expired is DERIVED, never stored — a stored expiry state drifts out of sync with
    /// the clock. Issued-but-past-expiry reads as Expired and yields ERR-207, which is the one
    /// failure distinguishable from ERR-206.
    /// </summary>
    public bool IsExpiredAt(DateTime utcNow) => Status == LeadershipCodeStatus.Issued && utcNow >= ExpiresAtUtc;

    /// <summary>BRL-077: redeemable means Issued AND not yet expired. Nothing else.</summary>
    public bool IsRedeemableAt(DateTime utcNow) => Status == LeadershipCodeStatus.Issued && utcNow < ExpiresAtUtc;

    /// <summary>
    /// IAuditable — every issue, revoke and consume writes an AuditLog row through CT-003's
    /// interceptor, which is CT-801's DoD and the audit half of SEC-009's compensating control for
    /// the accepted PlatformOperator bootstrap escalation (U47).
    /// </summary>
    public string GetAuditEntityId() => Id.ToString();
}

/// <summary>§2.12a: Consumed and Revoked are terminal. Expired is derived, never stored.</summary>
public enum LeadershipCodeStatus
{
    Issued,
    Consumed,
    Revoked
}
