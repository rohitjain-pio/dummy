using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolSync.Application.Auth;
using SchoolSync.Domain.Entities;

namespace SchoolSync.Infrastructure.Auth;

// 09-security-spec.md §1: claims sub/role/institution_id; 30-min access token (CFG-002, D2).
public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(30); // CFG-002

    public string IssueAccessToken(UserAccount user)
    {
        var jwt = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim("institution_id", user.InstitutionId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(AccessTokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string IssueRefreshTokenValue()
    {
        // 256 bits of randomness, URL-safe — this is the raw value handed to the caller; only
        // its hash (below) is ever persisted, matching the same principle as PasswordHash.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
