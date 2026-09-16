using Ekipzen.Domain.Entities.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Ekipzen.Infrastructure.Identity;

/// <summary>
/// JWT erişim token'ı ve refresh token üretimi. Kısa claim adları kullanılır
/// (sub, name, email, role, employeeId); API tarafında MapInboundClaims kapalıdır.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(
        ApplicationUser user, IEnumerable<string> roles)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            // Multi-tenant: aktif kiracı token'a gömülür; her istekte ICurrentTenant bunu okur.
            new("tenant", user.TenantId.ToString()),
        };

        if (user.Email is not null)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        if (user.EmployeeId is not null)
        {
            claims.Add(new Claim("employeeId", user.EmployeeId.Value.ToString()));
        }

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    /// <summary>Kriptografik rastgele refresh token üretir; DB'de yalnız hash'i saklanır.</summary>
    public (string RawToken, RefreshToken Entity) CreateRefreshToken(string userId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var entity = new RefreshToken
        {
            Token = Hash(rawToken),
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
        };

        return (rawToken, entity);
    }

    /// <summary>Ham refresh token'ın DB'deki karşılığını bulmak için SHA-256 hash'i.</summary>
    public static string Hash(string rawToken)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
