using SchoolSync.Infrastructure.Auth;
using Xunit;

namespace SchoolSync.IntegrationTests.Auth;

// CFG-001 / BRL-014. Adversarial: each "rejected" case is chosen to be one character away from
// passing, so a policy that's accidentally too lenient (e.g. off-by-one on length, or only 2 of
// the 4 character classes) would fail these, not just agree with whatever the code happens to do.
public class PasswordPolicyTests
{
    private readonly PasswordPolicy _policy = new();

    [Theory]
    [InlineData("Sh0rt!ab")] // 8 chars: 2 under the 10-char minimum
    [InlineData("lowercase123")] // 12 chars but only 2 classes (lower+digit) — no upper, no symbol
    [InlineData("Password123!")] // 12 chars, all 4 classes, but ON the blocklist
    public void Rejects_passwords_that_are_one_condition_away_from_passing(string password)
    {
        Assert.False(_policy.IsSatisfiedBy(password));
    }

    [Fact]
    public void Accepts_a_password_meeting_every_condition()
    {
        Assert.True(_policy.IsSatisfiedBy("Correct-Horse9"));
    }

    [Fact]
    public void Rejects_null_or_empty()
    {
        Assert.False(_policy.IsSatisfiedBy(""));
    }
}
