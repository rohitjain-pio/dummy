using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SchoolSync.Application.Common;

namespace SchoolSync.Infrastructure.Auth;

/// <summary>
/// CT-801 / U45. `04-data-model.md` §2.12b.
///
/// **CFG-SEC-06 is a dedicated 32-byte secret, deliberately NOT the JWT signing key.** 1.0.4 said
/// "the JWT-signing-key-class secret", which names a class of secret rather than a secret. If a
/// builder resolved that the obvious way — reusing the JWT key — then rotating it (routine, and
/// mandatory after a suspected token leak) would silently make every outstanding leadership code
/// fail with ERR-206, with nothing anywhere connecting the two. So: rotating the JWT key has no
/// effect on codes, and rotating this key has no effect on sessions.
///
/// **Fail-closed at construction.** No default, no fallback, no generated-if-missing: a defaulted
/// HMAC key is not a key, and a process that silently invents one produces codes that stop
/// resolving the next time it restarts. CT-801's DoD requires the app to refuse to start.
/// </summary>
public sealed class LeadershipCodeLookup : ILeadershipCodeLookup, IDisposable
{
    public const string KeyConfigPath = "Security:LeadershipCodeLookupKey"; // CFG-SEC-06
    private const int CodeLength = 8;            // CFG-010
    private const int MinimumKeyBytes = 32;      // §2.12b(a)

    // CFG-010's alphabet. No lookalikes (0/O, 1/I/L) — these are read off a screen and typed by a
    // human, and a code that fails because someone read O for 0 is indistinguishable from a leak.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly HMACSHA256 _hmac;

    public LeadershipCodeLookup(IConfiguration configuration)
    {
        var configured = configuration[KeyConfigPath];
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                $"{KeyConfigPath} (CFG-SEC-06) is not configured. The leadership-code lookup key has no " +
                "default and no fallback by design: a defaulted HMAC key is not a key, and inventing one " +
                "at startup would make every code issued by this process stop resolving after a restart. " +
                "Set it from the secret store (or user-secrets locally). See 04-data-model.md §2.12b(a).");

        var keyBytes = Encoding.UTF8.GetBytes(configured);
        if (keyBytes.Length < MinimumKeyBytes)
            throw new InvalidOperationException(
                $"{KeyConfigPath} (CFG-SEC-06) must be at least {MinimumKeyBytes} bytes; got {keyBytes.Length}.");

        _hmac = new HMACSHA256(keyBytes);
    }

    /// <summary>VAL-027: trim, then upper-case invariantly. The single definition — see the interface.</summary>
    public string Normalise(string code) => code.Trim().ToUpperInvariant();

    public string ComputeLookup(string code)
    {
        byte[] digest;
        // HMACSHA256 instances are not thread-safe, and this runs on the anonymous registration
        // path where concurrent redemptions are exactly what the rate limiter is sized for.
        lock (_hmac)
        {
            digest = _hmac.ComputeHash(Encoding.UTF8.GetBytes(Normalise(code)));
        }

        // Base64url, unpadded: 32 bytes -> 43 chars, which is what char(43) is sized for.
        return Convert.ToBase64String(digest)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string GenerateCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    public void Dispose() => _hmac.Dispose();
}
