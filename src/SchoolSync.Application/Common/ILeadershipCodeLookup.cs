namespace SchoolSync.Application.Common;

/// <summary>
/// CT-801 / U45. The keyed lookup for leadership codes — `04-data-model.md` §2.12b.
///
/// This is **new machinery**. `ParentLinkCode` stores its code in plaintext and needs nothing like
/// this; only the lifecycle was copied from it, not the storage (U45d).
/// </summary>
public interface ILeadershipCodeLookup
{
    /// <summary>
    /// VAL-027's normalisation — trim, then upper-case invariantly. **Defined once, here.** The
    /// spec is explicit that it must not be re-derived at the lookup site, because a code that
    /// normalises one way at validation and another at lookup would validate and then fail to
    /// resolve.
    /// </summary>
    string Normalise(string code);

    /// <summary>
    /// `BASE64URL(HMAC-SHA256(CFG-SEC-06, normalise(code)))` — 43 ASCII chars, unpadded, **never
    /// truncated**. Not truncating is deliberate: 43 characters is a trivial index key, so there is
    /// no width to buy and therefore no collision argument to have to make (§2.12b(b)).
    /// </summary>
    string ComputeLookup(string code);

    /// <summary>A fresh code's plaintext — CFG-010 length, alphanumeric. Returned to the caller exactly once, never stored.</summary>
    string GenerateCode();
}
