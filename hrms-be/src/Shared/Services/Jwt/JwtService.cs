using System.Security.Claims;
using System.Text;
using hrms.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace hrms.Shared.Services;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    // main app token (audience = Jwt:Audience)
    public (string Token, DateTime ExpiresAt) CreateToken(
        User user, string role, IEnumerable<Claim>? extraClaims = null)
        => Build(user, role, _config["Jwt:Audience"], extraClaims);

    // company app token (audience = Jwt:CompanyAudience)
    public (string Token, DateTime ExpiresAt) CreateCompanyToken(
        User user, string role, IEnumerable<Claim>? extraClaims = null)
        => Build(user, role, _config["Jwt:CompanyAudience"], extraClaims);

    private (string Token, DateTime ExpiresAt) Build(
        User user, string role, string? audience, IEnumerable<Claim>? extraClaims)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(
            int.Parse(_config["Jwt:ExpiryMinutes"] ?? "480"));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var claims = new List<Claim>
        {
            new("uid", user.Id.ToString()),
            new("sub", user.Email),
            new("name", user.Name),
            new("role", role)
        };

        if (extraClaims != null)
            claims.AddRange(extraClaims);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _config["Jwt:Issuer"],
            Audience = audience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return (token, expiresAt);
    }
}