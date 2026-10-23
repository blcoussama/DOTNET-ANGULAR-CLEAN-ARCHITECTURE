using System.Text;
using Microsoft.Extensions.Options;

namespace CreditFlow.Infrastructure.Identity;

/// <summary>Section <c>Jwt</c>. La clé de signature n'a pas de valeur par défaut : le démarrage est refusé sans elle.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; set; } = "CreditFlow";

    public string Audience { get; set; } = "CreditFlow";

    /// <summary>Clé HMAC-SHA256 (au moins 32 octets UTF-8). À fournir par variable d'environnement ou secret.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;

    public byte[] SigningKeyBytes() => Encoding.UTF8.GetBytes(SigningKey);
}

internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            failures.Add("Jwt:SigningKey est obligatoire (au moins 32 octets). Fournissez-la par variable d'environnement Jwt__SigningKey ou par user-secrets.");
        }
        else if (options.SigningKeyBytes().Length < JwtOptions.MinimumKeyBytes)
        {
            failures.Add($"Jwt:SigningKey est trop courte ({options.SigningKeyBytes().Length} octets) : 32 octets (256 bits) minimum.");
        }

        if (options.AccessTokenMinutes is < 1 or > 60)
        {
            failures.Add("Jwt:AccessTokenMinutes doit être compris entre 1 et 60.");
        }

        if (options.RefreshTokenDays is < 1 or > 30)
        {
            failures.Add("Jwt:RefreshTokenDays doit être compris entre 1 et 30.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Issuer et Jwt:Audience sont obligatoires.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
