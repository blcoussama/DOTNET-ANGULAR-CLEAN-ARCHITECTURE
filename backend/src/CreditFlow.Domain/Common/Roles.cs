namespace CreditFlow.Domain.Common;

/// <summary>Rôles applicatifs (noms exposés tels quels dans l'API).</summary>
public static class Roles
{
    public const string Client = "Client";
    public const string Analyst = "Analyst";
    public const string Admin = "Admin";

    public static IReadOnlyList<string> All { get; } = [Client, Analyst, Admin];

    public static bool IsValid(string? role) =>
        role is not null && All.Contains(role, StringComparer.Ordinal);

    public static bool IsReviewer(string? role) => role is Analyst or Admin;
}
