using SchoolSync.Application.Validation;

namespace SchoolSync.Application.Students;

public interface IStudentFieldPolicy
{
    bool IsSatisfiedBy(StudentFields fields, DateOnly today);
}

// VAL-008 through VAL-011 (07-business-logic/academic-records.md). Isolated from StudentService
// for the same reason CT-002 isolated PasswordPolicy from AuthService: the field rules carry an
// unresolved question, and confining it to one small type keeps the answer a one-file change.
//
// VAL-008/009 (uniqueness) are NOT here — uniqueness is a repository question, not a field-shape
// question, and lives in StudentService where the repository is available. This type only judges
// a payload in isolation.
public class StudentFieldPolicy : IStudentFieldPolicy
{
    // VAL-010 — PROVISIONAL, Q-203. The BRD states no numeric age bound; the spec's stated
    // provisional choice is to "reject DOB implying age <3 or >21 at enrolment as a data-entry-
    // error guard, not a hard business rule". 00-spec-index.md §12 still lists Q-203 as
    // unresolved (one of the 9 remaining non-blocking questions), so these two numbers are the
    // entirety of the guess and are named constants for exactly that reason. No CFG- key covers
    // this (12-infrastructure-spec.md lists CFG-001..008, none for enrolment age), so it is not
    // configuration-bound; when Q-203 is answered, the answer lands here and nowhere else.
    public const int MinPlausibleAgeYears = 3;
    public const int MaxPlausibleAgeYears = 21;

    public bool IsSatisfiedBy(StudentFields fields, DateOnly today)
    {
        // VAL-008/009: required. (Their uniqueness half is enforced in StudentService.)
        if (string.IsNullOrWhiteSpace(fields.SrNumber)) return false;
        if (string.IsNullOrWhiteSpace(fields.RollNumber)) return false;

        // SPEC-FR-011's remaining required fields.
        if (string.IsNullOrWhiteSpace(fields.Name)) return false;
        if (string.IsNullOrWhiteSpace(fields.Gender)) return false;
        if (!ContactFormats.IsValidEmail(fields.Email)) return false;

        // VAL-010: "valid past date" first — a DOB in the future is wrong regardless of the
        // provisional age window, and stays wrong however Q-203 is answered.
        if (fields.DateOfBirth >= today) return false;

        var age = AgeInYearsAt(fields.DateOfBirth, today);
        if (age < MinPlausibleAgeYears || age > MaxPlausibleAgeYears) return false;

        // VAL-011: guardian fields are optional (API-011 marks all four with `?`), but a supplied
        // value must satisfy the same VAL-001/VAL-003 rules as any other contact field.
        if (fields.GuardianContactEmail is not null && !ContactFormats.IsValidEmail(fields.GuardianContactEmail))
            return false;
        if (fields.GuardianPhone is not null && !ContactFormats.IsValidPhone(fields.GuardianPhone))
            return false;

        return true;
    }

    // Whole years elapsed, decremented when this year's birthday has not yet occurred — the
    // ordinary calendar definition of age, not a 365.25-day division.
    private static int AgeInYearsAt(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age;
    }
}
