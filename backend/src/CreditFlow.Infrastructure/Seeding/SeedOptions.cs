namespace CreditFlow.Infrastructure.Seeding;

/// <summary>
/// Section <c>Seed</c>. Les comptes de démonstration ne sont créés qu'en Development, et seulement
/// si leur mot de passe est fourni par la configuration (user-secrets, variables d'environnement) : jamais en dur.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool DemoData { get; set; } = true;

    public string AdminEmail { get; set; } = "admin@creditflow.local";

    public string? AdminPassword { get; set; }

    public string AnalystEmail { get; set; } = "analyste@creditflow.local";

    public string? AnalystPassword { get; set; }

    public string ClientEmail { get; set; } = "client@creditflow.local";

    public string? ClientPassword { get; set; }
}
