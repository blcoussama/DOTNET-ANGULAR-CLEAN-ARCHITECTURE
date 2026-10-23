using CreditFlow.Application.Common;

namespace CreditFlow.Api.Http;

/// <summary>Lecture stricte des énumérations passées en paramètre de requête ou de route (forme UNDER_REVIEW ou UnderReview).</summary>
internal sealed class QueryValues
{
    private readonly List<(string Field, string Message)> _errors = [];

    public TEnum? Enum<TEnum>(string? value, string field)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (EnumWire.TryParse<TEnum>(value, out var parsed))
        {
            return parsed;
        }

        _errors.Add((field, $"Valeur « {value} » invalide. Valeurs acceptées : {EnumWire.AllowedValues<TEnum>()}."));
        return null;
    }

    public void ThrowIfInvalid()
    {
        if (_errors.Count > 0)
        {
            throw ValidationExtensions.ValidationErrors(_errors);
        }
    }
}
