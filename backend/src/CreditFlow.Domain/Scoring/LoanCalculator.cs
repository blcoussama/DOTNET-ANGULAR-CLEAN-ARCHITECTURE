using CreditFlow.Domain.Common;

namespace CreditFlow.Domain.Scoring;

/// <summary>Résultat d'un calcul de prêt à mensualités constantes.</summary>
public sealed record LoanQuote(decimal AnnualRate, decimal MonthlyPayment, decimal TotalCost);

/// <summary>
/// Calcul d'annuité constante, entièrement en <see cref="decimal"/> (pas d'arrondi binaire).
/// r = taux annuel / 12 / 100 ; M = P·r / (1 − (1 + r)^−n) ; si r = 0, M = P / n.
/// Mensualité et coût total arrondis au centime (arrondi commercial, demi-unité vers le haut).
/// </summary>
public static class LoanCalculator
{
    public static decimal MonthlyPayment(decimal principal, decimal annualRatePercent, int durationMonths)
    {
        if (principal <= 0)
        {
            throw new DomainValidationException("amount", "Le montant doit être strictement positif.");
        }

        if (durationMonths <= 0)
        {
            throw new DomainValidationException("durationMonths", "La durée doit être strictement positive.");
        }

        if (annualRatePercent < 0)
        {
            throw new DomainValidationException("annualRate", "Le taux ne peut pas être négatif.");
        }

        var monthlyRate = annualRatePercent / 12m / 100m;
        decimal payment;
        if (monthlyRate == 0m)
        {
            payment = principal / durationMonths;
        }
        else
        {
            // P·r / (1 − (1+r)^−n) = P·r·f / (f − 1) avec f = (1+r)^n
            var factor = Pow(1m + monthlyRate, durationMonths);
            payment = principal * monthlyRate * factor / (factor - 1m);
        }

        return RoundMoney(payment);
    }

    /// <summary>Coût total du crédit (intérêts) calculé à partir de la mensualité arrondie.</summary>
    public static decimal TotalCost(decimal principal, decimal monthlyPayment, int durationMonths) =>
        RoundMoney((monthlyPayment * durationMonths) - principal);

    public static LoanQuote Quote(decimal principal, decimal annualRatePercent, int durationMonths)
    {
        var payment = MonthlyPayment(principal, annualRatePercent, durationMonths);
        return new LoanQuote(annualRatePercent, payment, TotalCost(principal, payment, durationMonths));
    }

    public static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Puissance entière par exponentiation rapide (précision décimale conservée).</summary>
    internal static decimal Pow(decimal value, int exponent)
    {
        var result = 1m;
        var current = value;
        var remaining = exponent;
        while (remaining > 0)
        {
            if ((remaining & 1) == 1)
            {
                result *= current;
            }

            remaining >>= 1;
            if (remaining > 0)
            {
                current *= current;
            }
        }

        return result;
    }
}
