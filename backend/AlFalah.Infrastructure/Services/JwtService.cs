using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AlFalah.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AlFalah.Infrastructure.Services;

/// <summary>
/// JWT token generation and validation using configured signing key.
/// </summary>
public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateAccessToken(
        string userId,
        string username,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        int? activeSchoolId,
        string preferredLanguage)
    {
        var key = GetSigningKey();
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("preferred_language", preferredLanguage),
        };

        if (activeSchoolId.HasValue)
            claims.Add(new Claim("active_school_id", activeSchoolId.Value.ToString()));

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var permission in permissions)
            claims.Add(new Claim("permission", permission));

        var expiryMinutes = int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"] ?? "60");

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public string? GetUserIdFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = GetSigningKey();

            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = false // Allow expired tokens in refresh flow
            };

            var principal = handler.ValidateToken(token, parameters, out _);
            return principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }
        catch
        {
            return null;
        }
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }
}
