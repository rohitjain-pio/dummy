namespace SchoolSync.Infrastructure.Auth;

// Bound from configuration ("Jwt" section) — the signing key itself must come from an
// environment variable / user-secrets in every real environment (company.md §4); nothing here
// hardcodes a value.
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string SigningKey { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
}
