using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Common;
using CreditFlow.Application.Products;
using CreditFlow.Application.Profiles;
using CreditFlow.Application.Simulations;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Products;
using FluentValidation;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CreditFlow.Application.Tests;

public sealed class SimulationServiceTests
{
    private readonly ICreditProductRepository _products = Substitute.For<ICreditProductRepository>();
    private readonly SimulationService _service;

    public SimulationServiceTests()
    {
        foreach (var type in Enum.GetValues<ProductType>())
        {
            _products.GetAsync(type, Arg.Any<CancellationToken>()).Returns(TestData.Product(type));
        }

        _service = new SimulationService(_products, new SimulationRequestValidator(), Options.Create(new ScoringOptions()));
    }

    [Fact]
    public async Task Simulation_without_income_returns_payment_and_cost_only()
    {
        var result = await _service.SimulateAsync(new SimulationRequest(ProductType.Consommation, 100_000m, 60, null, null), default);

        result.AnnualRate.ShouldBe(7.5m);
        result.MonthlyPayment.ShouldBe(2003.79m);
        result.TotalCost.ShouldBe(20227.40m);
        result.DebtRatio.ShouldBeNull();
        result.WithinDebtThreshold.ShouldBeNull();
        result.DebtThreshold.ShouldBe(0.40m);
    }

    [Theory]
    [InlineData(10_000, 1_000, 0.3004, true)]
    [InlineData(5_000, 0, 0.4008, false)]
    public async Task Simulation_with_income_computes_the_debt_ratio(double income, double charges, double expectedRatio, bool within)
    {
        var result = await _service.SimulateAsync(new SimulationRequest(ProductType.Consommation, 100_000m, 60, (decimal)income, (decimal)charges), default);

        result.DebtRatio.ShouldBe((decimal)expectedRatio);
        result.WithinDebtThreshold.ShouldBe(within);
    }

    [Fact]
    public async Task Simulation_uses_the_configured_threshold()
    {
        var service = new SimulationService(_products, new SimulationRequestValidator(), Options.Create(new ScoringOptions { MaxDebtRatio = 0.45m }));
        var result = await service.SimulateAsync(new SimulationRequest(ProductType.Consommation, 100_000m, 60, 5_000m, null), default);
        result.WithinDebtThreshold.ShouldBe(true);
    }

    [Fact]
    public async Task Simulation_validates_product_bounds()
    {
        var ex = await Should.ThrowAsync<ValidationException>(() =>
            _service.SimulateAsync(new SimulationRequest(ProductType.Immobilier, 50_000m, 12, null, null), default));

        ex.Errors.Select(e => e.PropertyName).ShouldBe(["amount", "durationMonths"], ignoreOrder: true);
    }

    [Fact]
    public async Task Simulation_validates_income_and_charges()
    {
        var ex = await Should.ThrowAsync<ValidationException>(() =>
            _service.SimulateAsync(new SimulationRequest(ProductType.Auto, 50_000m, 24, 0m, -5m), default));

        ex.Errors.Select(e => e.PropertyName).ShouldBe(["MonthlyIncome", "MonthlyCharges"], ignoreOrder: true);
    }
}

public sealed class ProductServiceTests
{
    private readonly ICreditProductRepository _products = Substitute.For<ICreditProductRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ProductService _service;

    public ProductServiceTests() =>
        _service = new ProductService(_products, _unitOfWork, new UpdateProductRequestValidator(), new FixedTimeProvider(TestData.Now));

    [Fact]
    public async Task Update_replaces_the_product_settings()
    {
        var product = TestData.Product(ProductType.Auto);
        _products.GetAsync(ProductType.Auto, Arg.Any<CancellationToken>()).Returns(product);

        var dto = await _service.UpdateAsync(ProductType.Auto, new UpdateProductRequest(5.9m, 10_000m, 700_000m, 12, 96, false), default);

        dto.AnnualRate.ShouldBe(5.9m);
        dto.MaxDurationMonths.ShouldBe(96);
        dto.IsActive.ShouldBeFalse();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_of_an_unknown_product_is_not_found()
    {
        await Should.ThrowAsync<NotFoundException>(() =>
            _service.UpdateAsync(ProductType.Auto, new UpdateProductRequest(5m, 1_000m, 2_000m, 1, 12, true), default));
    }

    [Fact]
    public async Task Update_requires_every_field_and_coherent_bounds()
    {
        var ex = await Should.ThrowAsync<ValidationException>(() =>
            _service.UpdateAsync(ProductType.Auto, new UpdateProductRequest(null, 5_000m, 1_000m, 24, 12, null), default));

        ex.Errors.Select(e => e.PropertyName).ShouldBe(["AnnualRate", "MaxAmount", "MaxDurationMonths", "IsActive"], ignoreOrder: true);
    }

    [Fact]
    public async Task Active_listing_only_asks_for_active_products()
    {
        _products.ListAsync(true, Arg.Any<CancellationToken>()).Returns([TestData.Product(ProductType.Auto)]);

        var list = await _service.ListActiveAsync(default);

        list.Single().Type.ShouldBe(ProductType.Auto);
        await _products.DidNotReceive().ListAsync(false, Arg.Any<CancellationToken>());
    }
}

public sealed class ClientProfileServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly IClientProfileRepository _profiles = Substitute.For<IClientProfileRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ClientProfileService _service;

    public ClientProfileServiceTests()
    {
        var clock = new FixedTimeProvider(TestData.Now);
        _service = new ClientProfileService(_profiles, _unitOfWork, new UpdateClientProfileRequestValidator(clock), clock);
    }

    [Fact]
    public async Task Missing_profile_is_not_found() =>
        await Should.ThrowAsync<NotFoundException>(() => _service.GetAsync(_userId, default));

    [Fact]
    public async Task First_update_creates_the_profile()
    {
        var dto = await _service.UpdateAsync(_userId, new UpdateClientProfileRequest(
            new DateOnly(1985, 5, 20), "+212 600-000-000", "Rabat", EmploymentType.Fonctionnaire, null, 12_000m, 0m, 10), default);

        dto.IsComplete.ShouldBeTrue();
        _profiles.Received(1).Add(Arg.Is<ClientProfile>(p => p.UserId == _userId));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Partial_update_is_allowed_but_incomplete()
    {
        _profiles.GetAsync(_userId, Arg.Any<CancellationToken>()).Returns(TestData.CompleteProfile(_userId));

        var dto = await _service.UpdateAsync(_userId, new UpdateClientProfileRequest(null, null, "Tanger", null, null, null, null, null), default);

        dto.IsComplete.ShouldBeFalse();
        dto.City.ShouldBe("Tanger");
        _profiles.DidNotReceive().Add(Arg.Any<ClientProfile>());
    }
}
