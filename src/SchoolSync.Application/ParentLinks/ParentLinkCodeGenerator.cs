using System.Security.Cryptography;

namespace SchoolSync.Application.ParentLinks;

public interface IParentLinkCodeGenerator
{
    string Generate();
}

// CFG-005 / T-003 / VAL-012: exactly 8 characters, single-use, 7-day expiry. The length lives
// here; the expiry lives in ParentLinkService, which is the only thing that needs a clock.
// Constants rather than IConfiguration, matching CT-002's treatment of CFG-001 in PasswordPolicy.
public class ParentLinkCodeGenerator : IParentLinkCodeGenerator
{
    public const int CodeLength = 8; // CFG-005

    // A human has to read this off a message and type it in (PF-005 step 2: communicated
    // out-of-band), so the confusable pairs are removed outright — no 0/O, no 1/I/L. 31 symbols
    // over 8 positions is ~8.5e11 combinations, which is ample for a single-use credential that
    // also expires in 7 days and is checked against one specific student.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public string Generate()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            // RandomNumberGenerator, not Random: this is a credential. GetInt32 is rejection-
            // sampled internally, so it is unbiased even though 31 does not divide the range
            // evenly — a plain `% Alphabet.Length` would skew toward the first few letters.
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }
}
