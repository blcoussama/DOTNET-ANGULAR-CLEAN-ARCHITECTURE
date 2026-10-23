using CreditFlow.Api.Configuration;
using CreditFlow.Api.Http;
using CreditFlow.Application.Common;
using CreditFlow.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminUsersController(IUserAdministrationService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<AdminUserDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<AdminUserDto>> List(
        [FromQuery] string? role,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        users.ListAsync(new UserListQuery(page, pageSize, string.IsNullOrWhiteSpace(role) ? null : role, search), cancellationToken);

    /// <summary>Crée un compte Analyst ou Admin avec un mot de passe temporaire.</summary>
    [HttpPost]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AdminUserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await users.CreateAsync(request, cancellationToken);
        return Created($"/api/v1/admin/users/{user.Id}", user);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AdminUserDto> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        users.UpdateAsync(User.ToActor(), id, request, cancellationToken);
}
