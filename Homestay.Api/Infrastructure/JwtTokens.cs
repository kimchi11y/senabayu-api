using System.Security.Claims;
using System.Text;
using Homestay.Api.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Homestay.Api.Infrastructure;

public record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Issues and validates the admin login token. The signing key comes from configuration, never the repo.</summary>
public class JwtTokens(IConfiguration configuration, TimeProvider clock)
{
    private const string Issuer = "senabayu-api";
    private const string Audience = "senabayu-admin";
    private const string RoleClaim = "role";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public AccessToken Issue(AdminUser admin)
    {
        var expiresAt = clock.GetUtcNow().Add(Lifetime);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new Claim(RoleClaim, admin.Role)
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(configuration), SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expiresAt);
    }

    public static void Configure(JwtBearerOptions options, IConfiguration configuration)
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            // Resolved per token, so a missing key only breaks admin requests, not the public API.
            IssuerSigningKeyResolver = (_, _, _, _) => [SigningKey(configuration)],
            RoleClaimType = RoleClaim,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    // No fallback key: without real configuration, admin requests fail instead of accepting a guessable key.
    private static SymmetricSecurityKey SigningKey(IConfiguration configuration)
    {
        var key = configuration["Jwt:SigningKey"];
        if (string.IsNullOrEmpty(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be set and at least 32 bytes long.");

        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }
}
