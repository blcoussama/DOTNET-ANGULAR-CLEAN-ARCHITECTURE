using System.Text.Json;

namespace CreditFlow.Api.Errors;

/// <summary>Normalise les clés d'erreurs (« $.amount », « Amount ») en camelCase (« amount »).</summary>
internal static class ValidationErrorKeys
{
    public static string Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "request";
        }

        var trimmed = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key.TrimStart('$');
        if (trimmed.Length == 0)
        {
            return "request";
        }

        return string.Join('.', trimmed.Split('.').Select(part => JsonNamingPolicy.CamelCase.ConvertName(part)));
    }
}
