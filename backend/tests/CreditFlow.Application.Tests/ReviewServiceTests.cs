using CreditFlow.Application.Abstractions;
using CreditFlow.Application.Common;
using CreditFlow.Application.Review;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Common;
using CreditFlow.Domain.Scoring;
using FluentValidation;
using NSubstitute;
using SortDirection = CreditFlow.Application.Review.SortDirection;

namespace CreditFlow.Application.Tests;

public sealed class ReviewServiceTests
{
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly CurrentActor _analyst = new(Guid.NewGuid(), Roles.Analyst);
    private readonly CurrentActor _otherAnalyst = new(Guid.NewGuid(), Roles.Analyst);
    private readonly CurrentActor _admin = new(Guid.NewGuid(), Roles.Admin);
    private readonly ICreditApplicationRepository _applications = Substitute.For<ICreditApplicationRepository>();
    private readonly IClientProfileRepository _profiles = Substitute.For<IClientProfileRepository>();
    private readonly IUserDirectory _users = Substitute.For<IUserDirectory>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ReviewService _service;

    public ReviewServiceTests()
    {
        _users.GetAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(TestData.Users((_clientId, "Salma", "Alaoui"), (_analyst.UserId, "Youssef", "Idrissi")));
        _profiles.GetAsync(_clientId, Arg.Any<CancellationToken>()).Returns(TestData.CompleteProfile(_clientId));
        _service = new ReviewService(
            _applications, _profiles, _users, _unitOfWork,
            new ReviewQueueQueryValidator(), new DecisionRequestValidator(), new FixedTimeProvider(TestData.Now));
    }

    private CreditApplication Given(CreditApplication application)
    {
        _applications.GetAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        return application;
    }

    [Fact]
    public async Task Drafts_are_invisible_to_reviewers()
    {
        var draft = Given(TestData.Draft(_clientId));

        await Should.ThrowAsync<NotFoundException>(() => _service.GetAsync(draft.Id, default));
    }

    [Fact]
    public async Task Detail_exposes_client_profile_and_scoring_breakdown()
    {
        var application = Given(TestData.Submitted(_clientId));

        var detail = await _service.GetAsync(application.Id, default);

        detail.RiskScore.ShouldBe(97);
        detail.RiskGrade.ShouldBe(RiskGrade.A);
        detail.Client.ShouldNotBeNull().LastName.ShouldBe("Alaoui");
        detail.ClientProfile.ShouldNotBeNull().IsComplete.ShouldBeTrue();
        var breakdown = detail.ScoringBreakdown.ShouldNotBeNull();
        breakdown.Total.ShouldBe(97);
        (breakdown.DebtRatioPoints, breakdown.EmploymentPoints, breakdown.SeniorityPoints, breakdown.AmountToIncomePoints).ShouldBe((40, 22, 15, 20));
        breakdown.MaxScore.ShouldBe(100);
        breakdown.Criteria.Sum(c => c.Points).ShouldBe(97);
        breakdown.Criteria.Select(c => c.Criterion).ShouldBe(["DEBT_RATIO", "EMPLOYMENT_STABILITY", "SENIORITY", "AMOUNT_TO_INCOME"]);
        breakdown.WithinDebtThreshold.ShouldBeTrue();
    }

    [Fact]
    public async Task Assign_puts_the_application_under_review_for_the_caller()
    {
        var application = Given(TestData.Submitted(_clientId));

        var detail = await _service.AssignAsync(_analyst, application.Id, null, default);

        detail.Status.ShouldBe(ApplicationStatus.UnderReview);
        detail.AssignedAnalystId.ShouldBe(_analyst.UserId);
        detail.AssignedAnalystName.ShouldBe("Youssef Idrissi");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Assign_twice_is_an_invalid_transition()
    {
        var application = Given(TestData.UnderReview(_clientId, _analyst.UserId));

        await Should.ThrowAsync<InvalidStatusTransitionException>(() => _service.AssignAsync(_otherAnalyst, application.Id, null, default));
    }

    [Fact]
    public async Task Only_the_assigned_analyst_can_decide()
    {
        var application = Given(TestData.UnderReview(_clientId, _analyst.UserId));

        await Should.ThrowAsync<ForbiddenException>(() =>
            _service.DecideAsync(_otherAnalyst, application.Id, ReviewDecision.Approve, new DecisionRequest("OK"), null, default));
        application.Status.ShouldBe(ApplicationStatus.UnderReview);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Admin_can_decide_without_being_assigned()
    {
        var application = Given(TestData.UnderReview(_clientId, _analyst.UserId));

        var detail = await _service.DecideAsync(_admin, application.Id, ReviewDecision.Reject, new DecisionRequest("Endettement"), null, default);

        detail.Status.ShouldBe(ApplicationStatus.Rejected);
        detail.DecisionComment.ShouldBe("Endettement");
    }

    [Theory]
    [InlineData(ReviewDecision.Approve, ApplicationStatus.Approved)]
    [InlineData(ReviewDecision.Reject, ApplicationStatus.Rejected)]
    [InlineData(ReviewDecision.RequestInfo, ApplicationStatus.InfoRequested)]
    public async Task Decisions_lead_to_the_expected_status(ReviewDecision decision, ApplicationStatus expected)
    {
        var application = Given(TestData.UnderReview(_clientId, _analyst.UserId));

        var detail = await _service.DecideAsync(_analyst, application.Id, decision, new DecisionRequest("Commentaire"), null, default);

        detail.Status.ShouldBe(expected);
        detail.Events[^1].Comment.ShouldBe("Commentaire");
        detail.Events[^1].ActorName.ShouldBe("Youssef Idrissi");
    }

    [Fact]
    public async Task Decision_requires_a_comment()
    {
        var application = Given(TestData.UnderReview(_clientId, _analyst.UserId));

        var ex = await Should.ThrowAsync<ValidationException>(() =>
            _service.DecideAsync(_analyst, application.Id, ReviewDecision.Approve, new DecisionRequest(" "), null, default));
        ex.Errors.ShouldContain(e => e.PropertyName == "Comment");
    }

    [Fact]
    public async Task Deciding_a_submitted_application_is_an_invalid_transition_before_any_permission_check()
    {
        var application = Given(TestData.Submitted(_clientId));

        await Should.ThrowAsync<InvalidStatusTransitionException>(() =>
            _service.DecideAsync(_otherAnalyst, application.Id, ReviewDecision.Approve, new DecisionRequest("OK"), null, default));
    }

    [Fact]
    public async Task Queue_resolves_assigned_to_me_into_the_current_user()
    {
        _applications.ListForReviewAsync(Arg.Any<ReviewQueueFilter>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<ReviewQueueItemDto>([], 1, 20, 0));

        await _service.GetQueueAsync(_analyst, new ReviewQueueQuery(AssignedToMe: true, Search: "  CF-2026  ", SortBy: ReviewSortField.RiskScore, SortDir: SortDirection.Desc), default);

        await _applications.Received(1).ListForReviewAsync(
            Arg.Is<ReviewQueueFilter>(f => f.AssignedTo == _analyst.UserId && f.Search == "CF-2026" && f.SortBy == ReviewSortField.RiskScore && f.SortDir == SortDirection.Desc),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Queue_refuses_the_draft_filter() =>
        await Should.ThrowAsync<ValidationException>(() =>
            _service.GetQueueAsync(_analyst, new ReviewQueueQuery(Status: ApplicationStatus.Draft), default));
}
