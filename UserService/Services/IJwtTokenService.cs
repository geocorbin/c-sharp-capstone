using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using UserService.Models;

namespace UserService.Services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
    int ExpiresInSeconds { get; }
}

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public int ExpiresInSeconds { get; } = configuration.GetValue("Jwt:ExpiresInSeconds", 86400);

    public string GenerateToken(User user)
    {
        var secret = configuration["Jwt:Secret"]
                     ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
        
        var claims = new[]
        {
            new Claim("userId", user.UserId.ToString()),
            new Claim("email", user.Email),
            new Claim("role", user.Role.ToString().ToUpperInvariant()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(ExpiresInSeconds),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}