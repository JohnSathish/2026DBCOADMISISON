using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Admissions.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Authentication;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    public JwtTokenResult GenerateToken(StudentApplicantAccount account, TimeSpan? lifetime = null)
    {
        var secretBytes = string.IsNullOrEmpty(_settings.Secret)
            ? Array.Empty<byte>()
            : Encoding.UTF8.GetBytes(_settings.Secret);
        // HS256 requires key size > 256 bits (IdentityModel validates byte length, not character count).
        if (secretBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT secret must be at least 32 bytes in UTF-8 (add characters if needed). Configure Authentication:Jwt:Secret.");
        }

        var key = new SymmetricSecurityKey(secretBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Claim value must not be null or JwtSecurityTokenHandler can throw.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, account.Email ?? string.Empty),
            new("unique_id", account.UniqueId ?? string.Empty),
            new("name", account.FullName ?? string.Empty),
            new(ClaimTypes.Role, "Applicant")
        };

        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(_settings.ExpiryMinutes));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtTokenResult(tokenString, expires);
    }
}

