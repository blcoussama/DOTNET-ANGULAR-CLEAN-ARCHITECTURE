using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;

namespace CreditFlow.Domain.Tests;

public sealed class ReferenceEntitiesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_products_match_the_specification()
    {
        var products = CreditProduct.Defaults(Now).ToDictionary(p => p.Type);

        products[ProductType.Consommation].ShouldSatisfyAllConditions(
            p => p.AnnualRate.ShouldBe(7.5m),
            p => p.MinAmount.ShouldBe(5_000m),
            p => p.MaxAmount.ShouldBe(300_000m),
            p => p.MinDurationMonths.ShouldBe(6),
            p => p.MaxDurationMonths.ShouldBe(84));
        products[ProductType.Auto].ShouldSatisfyAllConditions(
            p => p.AnnualRate.ShouldBe(6.5m),
            p => p.MinAmount.ShouldBe(20_000m),
            p => p.MaxAmount.ShouldBe(600_000m),
            p => p.MinDurationMonths.ShouldBe(12),
            p => p.MaxDurationMonths.ShouldBe(84));
        products[ProductType.Immobilier].ShouldSatisfyAllConditions(
            p => p.AnnualRate.ShouldBe(4.9m),
            p => p.MinAmount.ShouldBe(100_000m),
            p => p.MaxAmount.ShouldBe(5_000_000m),
            p => p.MinDurationMonths.ShouldBe(60),
            p => p.MaxDurationMonths.ShouldBe(300));
        products.Values.ShouldAllBe(p => p.IsActive);
    }

    [Theory]
    [InlineData(4_999, 12, false, true)]
    [InlineData(5_000, 6, true, true)]
    [InlineData(300_000, 84, true, true)]
    [InlineData(300_001, 85, false, false)]
    [InlineData(10_000, 5, true, false)]
    public void Product_bounds_are_inclusive(double amount, int months, bool amountOk, bool durationOk)
    {
        var product = CreditProduct.Defaults(Now).Single(p => p.Type == ProductType.Consommation);

        product.IsAmountAllowed((decimal)amount).ShouldBe(amountOk);
        product.IsDurationAllowed(months).ShouldBe(durationOk);
    }

    [Theory]
    [InlineData(-1, 1000, 2000, 1, 12, "annualRate")]
    [InlineData(31, 1000, 2000, 1, 12, "annualRate")]
    [InlineData(5, 0, 2000, 1, 12, "minAmount")]
    [InlineData(5, 3000, 2000, 1, 12, "maxAmount")]
    [InlineData(5, 1000, 2000, 0, 12, "minDurationMonths")]
    [InlineData(5, 1000, 2000, 24, 12, "maxDurationMonths")]
    [InlineData(5, 1000, 2000, 1, 481, "maxDurationMonths")]
    public void Product_update_enforces_its_invariants(double rate, double min, double max, int minMonths, int maxMonths, string field)
    {
        var product = CreditProduct.Defaults(Now)[0];
        Should.Throw<DomainValidationException>(() =>
            product.Update((decimal)rate, (decimal)min, (decimal)max, minMonths, maxMonths, true, Now)).Field.ShouldBe(field);
    }

    [Fact]
    public void Product_messages_mention_the_bounds()
    {
        var product = CreditProduct.Defaults(Now).Single(p => p.Type == ProductType.Auto);
        product.DurationRangeMessage().ShouldContain("12 et 84 mois");
        product.AmountRangeMessage().ShouldContain("20\u202F000 et 600\u202F000 MAD");
    }

    [Fact]
    public void A_new_profile_is_incomplete()
    {
        ClientProfile.CreateFor(Guid.NewGuid(), Now).IsComplete.ShouldBeFalse();
    }

    [Fact]
    public void Profile_is_complete_when_every_scoring_field_is_set_employer_being_optional()
    {
        var profile = ClientProfile.CreateFor(Guid.NewGuid(), Now);

        profile.Update(new DateOnly(1990, 1, 1), " 0600000000 ", "Tanger", EmploymentType.Cdi, null, 10_000m, 0m, 0, Now);

        profile.IsComplete.ShouldBeTrue();
        profile.Phone.ShouldBe("0600000000");
    }

    [Fact]
    public void Profile_without_income_is_incomplete()
    {
        var profile = ClientProfile.CreateFor(Guid.NewGuid(), Now);
        profile.Update(new DateOnly(1990, 1, 1), "0600000000", "Tanger", EmploymentType.Cdi, null, null, 0m, 3, Now);
        profile.IsComplete.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 0, 0, "monthlyIncome")]
    [InlineData(1000, -1, 0, "monthlyCharges")]
    [InlineData(1000, 0, -1, "yearsAtCurrentJob")]
    public void Profile_rejects_invalid_amounts(double income, double charges, int years, string field)
    {
        var profile = ClientProfile.CreateFor(Guid.NewGuid(), Now);
        Should.Throw<DomainValidationException>(() =>
            profile.Update(null, null, null, null, null, (decimal)income, (decimal)charges, years, Now)).Field.ShouldBe(field);
    }

    [Fact]
    public void Roles_helpers()
    {
        Roles.IsValid("Analyst").ShouldBeTrue();
        Roles.IsValid("analyst").ShouldBeFalse();
        Roles.IsReviewer(Roles.Admin).ShouldBeTrue();
        Roles.IsReviewer(Roles.Client).ShouldBeFalse();
    }
}
