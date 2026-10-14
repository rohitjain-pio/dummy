using System.Net.Mail;

namespace SchoolSync.Application.Validation;

// Shared contact-field format rules. VAL-001 (email) and VAL-003 (telephone) are declared in
// 07-business-logic/institutions.md and then re-used by citation rather than restatement:
// VAL-011 (07-business-logic/academic-records.md) defines guardian_contact_email/guardian_phone
// as "same format rules as VAL-001/VAL-003, applied per-guardian", and CT-104's teacher
// contact_email/telephone inherit the same pair. Keeping one implementation here is what makes
// "same rules" true in the code rather than a comment three files apart.
public static class ContactFormats
{
    // VAL-003 — PROVISIONAL, Q-201. The BRD and Tech Design name no specific phone format; the
    // spec's stated provisional choice is "E.164-tolerant loose validation, min 7 / max 15
    // digits, optional `+` prefix". 00-spec-index.md §12 still lists Q-201 as unresolved, so
    // these two bounds are the whole of the provisional decision and are deliberately isolated
    // here as named constants: answering Q-201 is a one-line edit in one file, not a sweep
    // through every caller. No CFG- key exists for this (12-infrastructure-spec.md §CFG lists
    // CFG-001..008, none covering phone format), so this is not bound to configuration.
    public const int MinPhoneDigits = 7;
    public const int MaxPhoneDigits = 15;

    // VAL-001: RFC-5322 format. MailAddress is the framework's own RFC-5322 parser — preferred
    // over a hand-rolled regex, which is the classic way to reject valid addresses.
    public static bool IsValidEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return MailAddress.TryCreate(value, out _);
    }

    // VAL-003 (provisional, Q-201). Separators are tolerated and ignored; the digit count is
    // what is actually bounded.
    public static bool IsValidPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var candidate = value.Trim();
        if (candidate.StartsWith('+')) candidate = candidate[1..];

        var digits = 0;
        foreach (var c in candidate)
        {
            if (char.IsDigit(c)) { digits++; continue; }
            // Ordinary human separators only — anything else (letters, a second '+') is a reject.
            if (c is ' ' or '-' or '(' or ')' or '.') continue;
            return false;
        }

        return digits is >= MinPhoneDigits and <= MaxPhoneDigits;
    }
}
