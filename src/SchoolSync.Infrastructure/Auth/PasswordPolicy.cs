using SchoolSync.Application.Auth;

namespace SchoolSync.Infrastructure.Auth;

// CFG-001 / D1 / BRL-014: min 10 chars, >=3 of 4 character classes, blocklist-checked, no
// forced rotation (rotation isn't this class's concern — there's no periodic-expiry check here).
public class PasswordPolicy : IPasswordPolicy
{
    private const int MinLength = 10;
    private const int MinClasses = 3;

    // The spec names "a blocklist" (BRL-014) without enumerating it — a routine technical detail
    // (CT-002 silence report), not a genuine fork. Seeded with the handful of passwords that
    // would otherwise trivially satisfy the 4-classes rule while still being a known-common
    // choice; swap for a real breached-password list (e.g. HaveIBeenPwned's range API) without
    // changing this type's contract.
    private static readonly HashSet<string> Blocklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password123!", "P@ssw0rd123", "Welcome123!", "Qwerty123!", "Admin@12345"
    };

    public bool IsSatisfiedBy(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            return false;

        var classes = 0;
        if (password.Any(char.IsUpper)) classes++;
        if (password.Any(char.IsLower)) classes++;
        if (password.Any(char.IsDigit)) classes++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) classes++;

        if (classes < MinClasses)
            return false;

        return !Blocklist.Contains(password);
    }
}
