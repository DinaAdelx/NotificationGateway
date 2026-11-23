using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NotificationGateway.Application.Abstractions;
using NotificationGateway.Domain.Entities;

namespace NotificationGateway.Infrastructure.Security;

public class JwtTokenService(IConfiguration config) : IJwtTokenService
{
    public string GenerateToken(User user)
    {
        var secretKey = config["Jwt:SecretKey"] ?? "GenericNotificationGatewaySuperSecretKey_2026_ReplaceInProduction!";
        var issuer = config["Jwt:Issuer"] ?? "NotificationGateway";
        var audience = config["Jwt:Audience"] ?? "NotificationGatewayClients";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("tenant_id", user.TenantId.ToString()),
            new("app_id", user.ApplicationId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
