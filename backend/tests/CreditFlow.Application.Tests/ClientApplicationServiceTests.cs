using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Applications;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Clients;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;
using FluentValidation;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CreditFlow.Application.Tests;

public sealed class ClientApplicationServiceTests
{
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly ICreditApplicationRepository _applications = Substitute.For<ICreditApplicationRepository>();
    private readonly ICreditProductRepository _products = Substitute.For<ICreditProductRepository>();
    private readonly IClientProfileRepository _profiles = Substitute.For<IClientProfileRepository>();
    private readonly IReferenceGenerator _references = Substitute.For<IReferenceGenerator>();
    private readonly IUserDirectory _users = Substitute.For<IUserDirectory>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ClientApplicationService _service;

    public ClientApplicationServiceTests()
    {
        foreach (var type in Enum.GetValues<ProductType>())
        {
            _products.GetAsync(type, Arg.Any<CancellationToken>()).Returns(TestData.Product(type));
        }

        _references.NextAsync(2026, Arg.Any<CancellationToken>()).Returns("CF-2026-000123");
        _users.GetAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(TestData.Users((_clientId, "Salma", "Alaoui")));
        _service = new ClientApplicationService(
            _applications, _products, _profiles, _references, _users, _unitOfWork,
            new SaveApplicationRequestValidator(), new CancelApplicationRequestValidator(), new ApplicationListQueryValidator(),
            Options.Create(new ScoringOptions()), new FixedTimeProvider(TestData.Now));
    }

    [Fact]
    public async Task Create_saves_a_draft_with_a_generated_reference()
    {
        var result = await _service.CreateAsync(_clientId, new SaveApplicationRequest(ProductType.Auto, 80_000m, 48, "Voiture"), default);

        result.Reference.ShouldBe("CF-2026-000123");
        result.Status.ShouldBe(ApplicationStatus.Draft);
        result.Events.Single().ActorName.ShouldBe("Salma Alaoui");
        _applications.Received(1).Add(Arg.Is<CreditApplication>(a => a.ClientId == _clientId && a.Amount == 80_000m));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(4_000, 24, "amount")]
    [InlineData(300_001, 24, "amount")]
    [InlineData(10_000, 5, "durationMonths")]
    [InlineData(10_000, 85, "durationMonths")]
    public async Task Create_enforces_the_product_bounds(double amount, int months, string field)
    {
        var ex = await Should.ThrowAsync<ValidationException>(() =>
            _service.CreateAsync(_clientId, new SaveApplicationRequest(ProductType.Consommation, (decimal)amount, months, null), default));

        ex.Errors.ShouldContain(e => e.PropertyName == field);
        _applications.DidNotReceive().Add(Arg.Any<CreditApplication>());
    }

    [Fact]
    public async Task Create_refuses_an_inactive_product()
    {
        var product = TestData.Product(ProductType.Auto);
        product.Update(product.AnnualRate, product.MinAmount, product.MaxAmount, product.MinDurationMonths, product.MaxDurationMonths, false, TestData.Now);
        _products.GetAsync(ProductType.Auto, Arg.Any<CancellationToken>()).Returns(product);

        var ex = await Should.ThrowAsync<ValidationException>(() =>
            _service.CreateAsync(_clientId, new SaveApplicationRequest(ProductType.Auto, 50_000m, 24, null), default));
        ex.Errors.ShouldContain(e => e.PropertyName == "productType");
    }

    [Fact]
    public async Task Create_validates_required_fields()
    {
        var ex = await Should.ThrowAsync<ValidationException>(() =>
            _service.CreateAsync(_clientId, new SaveApplicationRequest(null, null, null, null), default));

        ex.Errors.Select(e => e.PropertyName).ShouldBe(["ProductType", "Amount", "DurationMonths"], ignoreOrder: true);
    }

    [Fact]
    public async Task Get_returns_404_for_an_application_of_another_client()
    {
        var other = TestData.Draft(Guid.NewGuid());
        _applications.GetAsync(other.Id, Arg.Any<CancellationToken>()).Returns(other);

        await Should.ThrowAsync<NotFoundException>(() => _service.GetAsync(_clientId, other.Id, default));
    }

    [Fact]
    public async Task Client_detail_hides_the_internal_risk_score()
    {
        var application = TestData.Submitted(_clientId);
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var detail = await _service.GetAsync(_clientId, application.Id, default);

        detail.MonthlyPayment.ShouldBe(2003.79m);
        detail.RiskScore.ShouldBeNull();
        detail.RiskGrade.ShouldBeNull();
        detail.Recommendation.ShouldBeNull();
        detail.Documents.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Update_with_a_stale_version_is_a_conflict()
    {
        var application = TestData.Draft(_clientId);
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await Should.ThrowAsync<DomainConflictException>(() =>
            _service.UpdateAsync(_clientId, application.Id, new SaveApplicationRequest(ProductType.Consommation, 20_000m, 24, null), Guid.NewGuid(), default));
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_of_a_submitted_application_is_a_conflict()
    {
        var application = TestData.Submitted(_clientId);
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var ex = await Should.ThrowAsync<DomainConflictException>(() =>
            _service.UpdateAsync(_clientId, application.Id, new SaveApplicationRequest(ProductType.Consommation, 20_000m, 24, null), null, default));
        ex.Code.ShouldBe("not_editable");
    }

    [Fact]
    public async Task Submit_requires_a_complete_profile_and_both_documents()
    {
        var application = TestData.Draft(_clientId);
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        _profiles.GetAsync(_clientId, Arg.Any<CancellationToken>()).Returns(ClientProfile.CreateFor(_clientId, TestData.Now));

        var ex = await Should.ThrowAsync<ValidationException>(() => _service.SubmitAsync(_clientId, application.Id, null, default));

        ex.Errors.Select(e => e.PropertyName).Distinct().ShouldBe(["profile", "documents"], ignoreOrder: true);
        ex.Errors.Count(e => e.PropertyName == "documents").ShouldBe(2);
        application.Status.ShouldBe(ApplicationStatus.Draft);
    }

    [Fact]
    public async Task Submit_computes_and_freezes_the_scoring_with_the_current_rate()
    {
        var application = TestData.DraftWithDocuments(_clientId);
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        _profiles.GetAsync(_clientId, Arg.Any<CancellationToken>()).Returns(TestData.CompleteProfile(_clientId));

        var detail = await _service.SubmitAsync(_clientId, application.Id, application.Version, default);

        detail.Status.ShouldBe(ApplicationStatus.Submitted);
        detail.AnnualRate.ShouldBe(7.5m);
        detail.MonthlyPayment.ShouldBe(2003.79m);
        detail.DebtRatio.ShouldBe(0.3004m);
        application.RiskScore.ShouldBe(97);
        application.RiskGrade.ShouldBe(RiskGrade.A);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_of_an_approved_application_is_an_invalid_transition()
    {
        var application = TestData.UnderReview(_clientId, Guid.NewGuid());
        application.Approve(Guid.NewGuid(), Roles.Admin, "OK", TestData.Now);
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        await Should.ThrowAsync<InvalidStatusTransitionException>(() => _service.SubmitAsync(_clientId, application.Id, null, default));
        await _profiles.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Submit_rechecks_bounds_if_the_product_changed_since_the_draft()
    {
        var application = TestData.DraftWithDocuments(_clientId);
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        _profiles.GetAsync(_clientId, Arg.Any<CancellationToken>()).Returns(TestData.CompleteProfile(_clientId));
        var product = TestData.Product(ProductType.Consommation);
        product.Update(7.5m, 5_000m, 50_000m, 6, 84, true, TestData.Now);
        _products.GetAsync(ProductType.Consommation, Arg.Any<CancellationToken>()).Returns(product);

        var ex = await Should.ThrowAsync<ValidationException>(() => _service.SubmitAsync(_clientId, application.Id, null, default));
        ex.Errors.ShouldContain(e => e.PropertyName == "amount");
    }

    [Fact]
    public async Task Cancel_records_the_optional_comment()
    {
        var application = TestData.Submitted(_clientId);
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        var detail = await _service.CancelAsync(_clientId, application.Id, new CancelApplicationRequest("Plus besoin"), null, default);

        detail.Status.ShouldBe(ApplicationStatus.Cancelled);
        detail.Events[^1].Comment.ShouldBe("Plus besoin");
    }

    [Fact]
    public async Task List_rejects_a_page_size_over_100() =>
        await Should.ThrowAsync<ValidationException>(() =>
            _service.ListAsync(_clientId, new ApplicationListQuery(null, 1, 101), default));

    [Fact]
    public async Task List_delegates_to_the_repository_with_the_filter()
    {
        var page = new PagedResult<ApplicationSummaryDto>([], 2, 10, 15);
        _applications.ListForClientAsync(_clientId, ApplicationStatus.Submitted, 2, 10, Arg.Any<CancellationToken>()).Returns(page);

        var result = await _service.ListAsync(_clientId, new ApplicationListQuery(ApplicationStatus.Submitted, 2, 10), default);

        result.ShouldBe(page);
        result.TotalPages.ShouldBe(2);
    }
}
