using CreditFlow.Api.Configuration;
using CreditFlow.Api.Http;
using CreditFlow.Application.Common;
using CreditFlow.Application.Dashboard;
using CreditFlow.Application.Review;
using CreditFlow.Domain.Applications;
using CreditFlow.Domain.Products;
using CreditFlow.Domain.Scoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Controllers;

/// <summary>Paramètres de la file : filtres, recherche, tri (<c>sortBy</c> = submittedAt | amount | riskScore, <c>sortDir</c> = asc | desc).</summary>
public sealed class ReviewQueueParameters
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = Paging.DefaultPageSize;

    public string? Status { get; set; }

    public string? RiskGrade { get; set; }

    public string? ProductType { get; set; }

    public bool AssignedToMe { get; set; }

    public string? Search { get; set; }

    public string? SortBy { get; set; }

    public string? SortDir { get; set; }
}

[ApiController]
[Route("api/v1/review/applications")]
[Authorize(Policy = Policies.Reviewer)]
public sealed class ReviewController(ReviewService review) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<ReviewQueueItemDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ReviewQueueItemDto>> Queue([FromQuery] ReviewQueueParameters parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var values = new QueryValues();
        var query = new ReviewQueueQuery(
            parameters.Page,
            parameters.PageSize,
            values.Enum<ApplicationStatus>(parameters.Status, "status"),
            values.Enum<RiskGrade>(parameters.RiskGrade, "riskGrade"),
            values.Enum<ProductType>(parameters.ProductType, "productType"),
            parameters.AssignedToMe,
            parameters.Search,
            values.Enum<ReviewSortField>(parameters.SortBy, "sortBy") ?? ReviewSortField.SubmittedAt,
            values.Enum<SortDirection>(parameters.SortDir, "sortDir") ?? SortDirection.Asc);
        values.ThrowIfInvalid();
        return review.GetQueueAsync(User.ToActor(), query, cancellationToken);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ReviewApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ReviewApplicationDetailDto> Get(Guid id, CancellationToken cancellationToken) =>
        WithVersion(await review.GetAsync(id, cancellationToken));

    [HttpPost("{id:guid}/assign")]
    [ProducesResponseType<ReviewApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ReviewApplicationDetailDto> Assign(Guid id, CancellationToken cancellationToken) =>
        WithVersion(await review.AssignAsync(User.ToActor(), id, Request.ExpectedVersion(), cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<ReviewApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ReviewApplicationDetailDto> Approve(Guid id, DecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(id, ReviewDecision.Approve, request, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<ReviewApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ReviewApplicationDetailDto> Reject(Guid id, DecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(id, ReviewDecision.Reject, request, cancellationToken);

    [HttpPost("{id:guid}/request-info")]
    [ProducesResponseType<ReviewApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ReviewApplicationDetailDto> RequestInfo(Guid id, DecisionRequest request, CancellationToken cancellationToken) =>
        DecideAsync(id, ReviewDecision.RequestInfo, request, cancellationToken);

    private async Task<ReviewApplicationDetailDto> DecideAsync(Guid id, ReviewDecision decision, DecisionRequest request, CancellationToken cancellationToken) =>
        WithVersion(await review.DecideAsync(User.ToActor(), id, decision, request, Request.ExpectedVersion(), cancellationToken));

    private ReviewApplicationDetailDto WithVersion(ReviewApplicationDetailDto dto)
    {
        Response.SetVersionHeader(dto.Version);
        return dto;
    }
}

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Policy = Policies.Reviewer)]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("stats")]
    [ProducesResponseType<DashboardStatsDto>(StatusCodes.Status200OK)]
    public Task<DashboardStatsDto> Stats(CancellationToken cancellationToken) => dashboard.GetStatsAsync(cancellationToken);
}
