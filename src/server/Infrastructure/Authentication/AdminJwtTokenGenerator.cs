using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Admissions.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Authentication;

public class AdminJwtTokenGenerator : IAdminJwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public AdminJwtTokenGenerator(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    public JwtTokenResult GenerateToken(AdminUser adminUser, TimeSpan? lifetime = null)
    {
        var secretBytes = string.IsNullOrEmpty(_settings.Secret)
            ? Array.Empty<byte>()
            : Encoding.UTF8.GetBytes(_settings.Secret);
        if (secretBytes.Length < 32)
        {
            throw new InvalidOperationException(
                "JWT secret must be at least 32 bytes in UTF-8 (add characters if needed). Configure Authentication:Jwt:Secret.");
        }

        var key = new SymmetricSecurityKey(secretBytes);
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, adminUser.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, adminUser.Email ?? string.Empty),
            new("username", adminUser.Username ?? string.Empty),
            new("name", adminUser.FullName ?? string.Empty),
            new(ClaimTypes.Role, "Admin")
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














