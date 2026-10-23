using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CreditFlow.Infrastructure.Identity;

public static class CreditFlowClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Role = "role";
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string GivenName = JwtRegisteredClaimNames.GivenName;
    public const string FamilyName = JwtRegisteredClaimNames.FamilyName;

    /// <summary>Empreinte du security stamp Identity : invalide le jeton d'accès dès qu'il change.</summary>
    public const string SecurityStamp = "sst";
}

/// <summary>Émission des jetons d'accès JWT et génération / hachage des refresh tokens.</summary>
public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    private readonly JsonWebTokenHandler _handler = new();

    public int AccessTokenLifetimeSeconds => options.Value.AccessTokenMinutes * 60;

    public string CreateAccessToken(ApplicationUser user, string role)
    {
        ArgumentNullException.ThrowIfNull(user);
        var jwt = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(jwt.AccessTokenMinutes),
            Subject = new ClaimsIdentity(
            [
                new Claim(CreditFlowClaims.Subject, user.Id.ToString()),
                new Claim(CreditFlowClaims.Email, user.Email ?? string.Empty),
                new Claim(CreditFlowClaims.GivenName, user.FirstName),
                new Claim(CreditFlowClaims.FamilyName, user.LastName),
                new Claim(CreditFlowClaims.Role, role),
                new Claim(CreditFlowClaims.SecurityStamp, StampFingerprint(user.SecurityStamp)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(jwt.SigningKeyBytes()), SecurityAlgorithms.HmacSha256),
        };
        return _handler.CreateToken(descriptor);
    }

    /// <summary>Jeton opaque de 256 bits, encodé base64url.</summary>
    public static string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));

    public static string HashRefreshToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string StampFingerprint(string? securityStamp) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(securityStamp ?? string.Empty)))[..22];
}
