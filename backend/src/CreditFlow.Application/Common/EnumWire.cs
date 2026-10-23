using System.Text.Json;

namespace CreditFlow.Application.Common;

/// <summary>
/// Représentation des énumérations sur le fil (JSON et paramètres de requête) : SNAKE_CASE majuscule
/// (ex. <c>UnderReview</c> ⇒ <c>UNDER_REVIEW</c>, <c>SansEmploi</c> ⇒ <c>SANS_EMPLOI</c>).
/// </summary>
public static class EnumWire
{
    public static JsonNamingPolicy NamingPolicy { get; } = JsonNamingPolicy.SnakeCaseUpper;

    public static string ToWire<TEnum>(TEnum value)
        where TEnum : struct, Enum => NamingPolicy.ConvertName(value.ToString());

    /// <summary>Accepte la forme fil (UNDER_REVIEW) ou le nom C# (UnderReview), sans tenir compte de la casse ; refuse les entiers.</summary>
    public static bool TryParse(Type enumType, string? text, out object? value)
    {
        ArgumentNullException.ThrowIfNull(enumType);
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var candidate = text.Trim();
        foreach (var name in Enum.GetNames(enumType))
        {
            if (string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NamingPolicy.ConvertName(name), candidate, StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse(enumType, name);
                return true;
            }
        }

        return false;
    }

    public static bool TryParse<TEnum>(string? text, out TEnum value)
        where TEnum : struct, Enum
    {
        if (TryParse(typeof(TEnum), text, out var parsed) && parsed is TEnum typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public static string AllowedValues<TEnum>()
        where TEnum : struct, Enum => string.Join(", ", Enum.GetValues<TEnum>().Select(v => ToWire(v)));
}
