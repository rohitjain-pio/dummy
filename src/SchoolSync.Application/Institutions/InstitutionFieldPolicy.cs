using SchoolSync.Application.Validation;

namespace SchoolSync.Application.Institutions;

// BRL-004 / VAL-001, VAL-003, VAL-004 (07-business-logic/institutions.md) plus the required
// columns of ENT-001 (04-data-model.md §2.1), judged on a payload in isolation. VAL-002 (the
// composite duplicate check) is NOT here — it needs the repository and stays in
// InstitutionService, the same split StudentFieldPolicy makes for VAL-008/009.
//
// Added for U7 (_upstream-feedback.md): CT-101 originally performed no field validation, so
// ERR-901 was unreachable on API-001.
public static class InstitutionFieldPolicy
{
    public const int PostalCodeLength = 6;

    public static bool IsSatisfiedBy(InstitutionFields fields)
    {
        // ENT-001 required columns with no VAL- rule of their own.
        if (string.IsNullOrWhiteSpace(fields.Name)) return false;
        if (string.IsNullOrWhiteSpace(fields.RegistrationNumber)) return false;
        if (string.IsNullOrWhiteSpace(fields.PostalAddress)) return false;
        if (string.IsNullOrWhiteSpace(fields.City)) return false;
        if (string.IsNullOrWhiteSpace(fields.Region)) return false;

        if (!ContactFormats.IsValidEmail(fields.ContactEmail)) return false; // VAL-001
        if (!ContactFormats.IsValidPhone(fields.Telephone)) return false; // VAL-003 (provisional, Q-201)
        if (!IsValidPostalCode(fields.PostalCode)) return false; // VAL-004

        return true;
    }

    // VAL-004: exactly 6 numeric digits. ASCII only — char.IsDigit would also accept other
    // scripts' digits (e.g. Devanagari), which a 6-digit postal code never contains.
    public static bool IsValidPostalCode(string? value) =>
        value is { Length: PostalCodeLength } && value.All(char.IsAsciiDigit);
}
