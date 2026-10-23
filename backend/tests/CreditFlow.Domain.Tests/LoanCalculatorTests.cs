using CreditFlow.Domain.Common;
using CreditFlow.Domain.Scoring;

namespace CreditFlow.Domain.Tests;

public sealed class LoanCalculatorTests
{
    // Valeurs de référence calculées à la main (formule d'annuité, arrondi au centime) et recoupées en précision 50 chiffres.
    [Theory]
    [InlineData(100_000, 7.5, 60, 2003.79, 20227.40)]
    [InlineData(50_000, 7.5, 36, 1555.31, 5991.16)]
    [InlineData(900_000, 4.9, 240, 5890.00, 513600.00)]
    [InlineData(20_000, 6.5, 12, 1725.93, 711.16)]
    [InlineData(5_000, 7.5, 6, 851.66, 109.96)]
    [InlineData(5_000_000, 4.9, 300, 28938.93, 3681679.00)]
    [InlineData(30_000, 7.5, 24, 1349.99, 2399.76)]
    [InlineData(250_000, 6.5, 84, 3712.36, 61838.24)]
    public void Quote_matches_hand_computed_annuity(double principal, double rate, int months, double expectedPayment, double expectedCost)
    {
        var quote = LoanCalculator.Quote((decimal)principal, (decimal)rate, months);

        quote.MonthlyPayment.ShouldBe((decimal)expectedPayment);
        quote.TotalCost.ShouldBe((decimal)expectedCost);
        quote.AnnualRate.ShouldBe((decimal)rate);
    }

    [Fact]
    public void Zero_rate_divides_principal_evenly()
    {
        LoanCalculator.MonthlyPayment(12_000m, 0m, 12).ShouldBe(1000.00m);
        LoanCalculator.Quote(12_000m, 0m, 12).TotalCost.ShouldBe(0m);
    }

    [Fact]
    public void Zero_rate_rounds_to_the_cent()
    {
        LoanCalculator.MonthlyPayment(10_000m, 0m, 3).ShouldBe(3333.33m);
    }

    [Fact]
    public void Total_cost_uses_the_rounded_monthly_payment()
    {
        LoanCalculator.TotalCost(100_000m, 2003.79m, 60).ShouldBe(20227.40m);
    }

    [Theory]
    [InlineData(0, 7.5, 12, "amount")]
    [InlineData(-1, 7.5, 12, "amount")]
    [InlineData(1000, 7.5, 0, "durationMonths")]
    [InlineData(1000, -1, 12, "annualRate")]
    public void Invalid_inputs_are_rejected(double principal, double rate, int months, string field)
    {
        var ex = Should.Throw<DomainValidationException>(() => LoanCalculator.MonthlyPayment((decimal)principal, (decimal)rate, months));
        ex.Field.ShouldBe(field);
    }

    [Theory]
    [InlineData(2, 0, 1)]
    [InlineData(2, 1, 2)]
    [InlineData(2, 10, 1024)]
    [InlineData(1.5, 3, 3.375)]
    public void Pow_computes_integer_powers_exactly(double value, int exponent, double expected)
    {
        LoanCalculator.Pow((decimal)value, exponent).ShouldBe((decimal)expected);
    }

    [Fact]
    public void Rounding_is_half_away_from_zero()
    {
        LoanCalculator.RoundMoney(1.005m).ShouldBe(1.01m);
        LoanCalculator.RoundMoney(1.004m).ShouldBe(1.00m);
    }
}
