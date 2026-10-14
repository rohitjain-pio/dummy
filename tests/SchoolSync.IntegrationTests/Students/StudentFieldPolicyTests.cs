using SchoolSync.Application.Students;
using Xunit;

namespace SchoolSync.IntegrationTests.Students;

// VAL-008 through VAL-011. The two age bounds under test are Q-203's PROVISIONAL choice, not a
// confirmed business rule — these tests pin the behaviour that currently ships so that answering
// Q-203 produces a visible, deliberate test change rather than a silent behavioural drift.
public class StudentFieldPolicyTests
{
    private static readonly DateOnly Today = new(2026, 9, 11);
    private readonly StudentFieldPolicy _policy = new();

    private static StudentFields Fields(
        string srNumber = "SR-1", string rollNumber = "ROLL-1", string name = "Asha Menon",
        DateOnly? dob = null, string gender = "Female", string email = "asha@example.edu",
        string? guardianEmail = null, string? guardianPhone = null) => new(
        srNumber, rollNumber, name, dob ?? Today.AddYears(-10), gender, email, Guid.NewGuid(),
        null, null, guardianEmail, guardianPhone);

    [Fact]
    public void A_complete_plausible_payload_is_accepted()
        => Assert.True(_policy.IsSatisfiedBy(Fields(), Today));

    [Theory] // VAL-008/009 and SPEC-FR-011's other required fields
    [InlineData("", "ROLL-1", "Asha", "Female", "asha@example.edu")]
    [InlineData("   ", "ROLL-1", "Asha", "Female", "asha@example.edu")]
    [InlineData("SR-1", "", "Asha", "Female", "asha@example.edu")]
    [InlineData("SR-1", "ROLL-1", "", "Female", "asha@example.edu")]
    [InlineData("SR-1", "ROLL-1", "Asha", "", "asha@example.edu")]
    [InlineData("SR-1", "ROLL-1", "Asha", "Female", "")]
    [InlineData("SR-1", "ROLL-1", "Asha", "Female", "not-an-email")]
    public void Missing_or_malformed_required_fields_are_rejected(
        string sr, string roll, string name, string gender, string email)
        => Assert.False(_policy.IsSatisfiedBy(Fields(sr, roll, name, email: email, gender: gender), Today));

    [Fact] // VAL-010's non-provisional half: a future DOB is wrong however Q-203 is answered
    public void A_date_of_birth_in_the_future_is_rejected()
        => Assert.False(_policy.IsSatisfiedBy(Fields(dob: Today.AddYears(1)), Today));

    [Fact]
    public void Today_as_a_date_of_birth_is_rejected()
        => Assert.False(_policy.IsSatisfiedBy(Fields(dob: Today), Today));

    [Theory] // VAL-010's PROVISIONAL bounds (Q-203): <3 or >21 at enrolment
    [InlineData(2, false)]
    [InlineData(3, true)] // boundary, inclusive
    [InlineData(21, true)] // boundary, inclusive
    [InlineData(22, false)]
    public void Age_is_bounded_by_the_provisional_plausibility_window(int ageYears, bool expected)
        => Assert.Equal(expected, _policy.IsSatisfiedBy(Fields(dob: Today.AddYears(-ageYears)), Today));

    [Fact] // The birthday-not-yet-reached case — a naive year subtraction gets this wrong
    public void Age_counts_whole_years_not_calendar_year_differences()
    {
        // Born 2023-12-31; on 2026-09-11 this person is 2, not 3. A year-subtraction
        // implementation would compute 3 and wrongly accept them.
        var dob = new DateOnly(2023, 12, 31);
        Assert.False(_policy.IsSatisfiedBy(Fields(dob: dob), Today));
    }

    [Theory] // VAL-011 -> VAL-001: guardian email, when supplied, is a real address
    [InlineData(null, true)] // optional (API-011 marks it `?`)
    [InlineData("guardian@example.edu", true)]
    [InlineData("not-an-email", false)]
    [InlineData("", false)]
    public void Guardian_email_when_supplied_must_be_well_formed(string? guardianEmail, bool expected)
        => Assert.Equal(expected, _policy.IsSatisfiedBy(Fields(guardianEmail: guardianEmail), Today));

    [Theory] // VAL-011 -> VAL-003 (PROVISIONAL, Q-201): 7-15 digits, optional '+', separators ignored
    [InlineData(null, true)] // optional
    [InlineData("+919000000001", true)]
    [InlineData("+91 90000 00001", true)]
    [InlineData("(080) 4000-1234", true)]
    [InlineData("123456", false)] // 6 digits — below the lower bound
    [InlineData("1234567890123456", false)] // 16 digits — above the upper bound
    [InlineData("+91-90000-0000x", false)] // a letter is not a separator
    public void Guardian_phone_when_supplied_must_match_the_provisional_contact_pattern(string? guardianPhone, bool expected)
        => Assert.Equal(expected, _policy.IsSatisfiedBy(Fields(guardianPhone: guardianPhone), Today));
}
