using System.Globalization;

namespace CreditFlow.Domain.Common;

/// <summary>
/// Format numérique français (1 234,56 ; 35,42 %) construit sans dépendre des données ICU,
/// afin de fonctionner aussi en mode « globalization invariant » (images Docker minimales).
/// </summary>
public static class FrenchFormat
{
    public static NumberFormatInfo Numbers { get; } = Create();

    private static NumberFormatInfo Create()
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.NumberDecimalSeparator = ",";
        format.NumberGroupSeparator = " ";
        format.PercentDecimalSeparator = ",";
        format.PercentGroupSeparator = " ";
        format.PercentPositivePattern = 0;
        format.PercentNegativePattern = 0;
        return NumberFormatInfo.ReadOnly(format);
    }
}
