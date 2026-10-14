using SchoolSync.Application.Auth;

namespace SchoolSync.Infrastructure.Auth;

public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // A malformed/foreign hash must fail closed, never throw past the auth boundary.
            return false;
        }
    }
}
