using CreditFlow.Domain.Applications;

namespace CreditFlow.Domain.Tests;

public sealed class ApplicationStateMachineTests
{
    private static readonly (ApplicationStatus From, ApplicationAction Action, ApplicationStatus To)[] Allowed =
    [
        (ApplicationStatus.Draft, ApplicationAction.Submit, ApplicationStatus.Submitted),
        (ApplicationStatus.InfoRequested, ApplicationAction.Submit, ApplicationStatus.Submitted),
        (ApplicationStatus.Submitted, ApplicationAction.Assign, ApplicationStatus.UnderReview),
        (ApplicationStatus.UnderReview, ApplicationAction.Approve, ApplicationStatus.Approved),
        (ApplicationStatus.UnderReview, ApplicationAction.Reject, ApplicationStatus.Rejected),
        (ApplicationStatus.UnderReview, ApplicationAction.RequestInfo, ApplicationStatus.InfoRequested),
        (ApplicationStatus.Draft, ApplicationAction.Cancel, ApplicationStatus.Cancelled),
        (ApplicationStatus.Submitted, ApplicationAction.Cancel, ApplicationStatus.Cancelled),
        (ApplicationStatus.InfoRequested, ApplicationAction.Cancel, ApplicationStatus.Cancelled),
    ];

    public static TheoryData<ApplicationStatus, ApplicationAction, ApplicationStatus> AllowedTransitions()
    {
        var data = new TheoryData<ApplicationStatus, ApplicationAction, ApplicationStatus>();
        foreach (var (from, action, to) in Allowed)
        {
            data.Add(from, action, to);
        }

        return data;
    }

    public static TheoryData<ApplicationStatus, ApplicationAction> ForbiddenTransitions()
    {
        var data = new TheoryData<ApplicationStatus, ApplicationAction>();
        foreach (var from in Enum.GetValues<ApplicationStatus>())
        {
            foreach (var action in Enum.GetValues<ApplicationAction>())
            {
                if (!Allowed.Any(t => t.From == from && t.Action == action))
                {
                    data.Add(from, action);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void Allowed_transition_leads_to_the_expected_status(ApplicationStatus from, ApplicationAction action, ApplicationStatus to)
    {
        ApplicationStateMachine.CanApply(from, action).ShouldBeTrue();
        ApplicationStateMachine.Next(from, action).ShouldBe(to);
    }

    [Theory]
    [MemberData(nameof(ForbiddenTransitions))]
    public void Any_other_transition_is_forbidden(ApplicationStatus from, ApplicationAction action)
    {
        ApplicationStateMachine.CanApply(from, action).ShouldBeFalse();
        var ex = Should.Throw<InvalidStatusTransitionException>(() => ApplicationStateMachine.Next(from, action));
        ex.From.ShouldBe(from);
        ex.Action.ShouldBe(action);
        ex.Code.ShouldBe("invalid_transition");
        ex.Message.ShouldContain(ApplicationStateMachine.Describe(from));
    }

    [Fact]
    public void Transition_table_has_exactly_nine_entries() =>
        Enum.GetValues<ApplicationStatus>().Sum(s => ApplicationStateMachine.AllowedActions(s).Count).ShouldBe(9);

    [Fact]
    public void Final_statuses_allow_no_action()
    {
        foreach (var status in new[] { ApplicationStatus.Approved, ApplicationStatus.Rejected, ApplicationStatus.Cancelled })
        {
            ApplicationStateMachine.IsFinal(status).ShouldBeTrue();
            ApplicationStateMachine.AllowedActions(status).ShouldBeEmpty();
        }
    }

    [Theory]
    [InlineData(ApplicationStatus.Draft, true)]
    [InlineData(ApplicationStatus.InfoRequested, true)]
    [InlineData(ApplicationStatus.Submitted, false)]
    [InlineData(ApplicationStatus.UnderReview, false)]
    [InlineData(ApplicationStatus.Approved, false)]
    public void Only_draft_and_info_requested_are_editable(ApplicationStatus status, bool editable) =>
        ApplicationStateMachine.IsEditable(status).ShouldBe(editable);

    [Fact]
    public void Every_status_and_action_has_a_french_label()
    {
        Enum.GetValues<ApplicationStatus>().ShouldAllBe(s => ApplicationStateMachine.Describe(s) != s.ToString());
        Enum.GetValues<ApplicationAction>().ShouldAllBe(a => ApplicationStateMachine.Describe(a) != a.ToString());
    }
}
