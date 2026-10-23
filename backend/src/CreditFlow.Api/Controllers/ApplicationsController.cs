using CreditFlow.Api.Configuration;
using CreditFlow.Api.Http;
using CreditFlow.Application.Applications;
using CreditFlow.Application.Common;
using CreditFlow.Application.Documents;
using CreditFlow.Domain.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Controllers;

/// <summary>Demandes du client connecté (un client n'accède qu'à ses propres demandes : 404 sinon).</summary>
[ApiController]
[Route("api/v1/applications")]
[Authorize(Policy = Policies.Client)]
public sealed class ApplicationsController(ClientApplicationService applications, DocumentService documents) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApplicationDetailDto>> Create(SaveApplicationRequest request, CancellationToken cancellationToken)
    {
        var created = await applications.CreateAsync(User.UserId(), request, cancellationToken);
        Response.SetVersionHeader(created.Version);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("mine")]
    [ProducesResponseType<PagedResult<ApplicationSummaryDto>>(StatusCodes.Status200OK)]
    public Task<PagedResult<ApplicationSummaryDto>> Mine(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var values = new QueryValues();
        var parsed = values.Enum<ApplicationStatus>(status, "status");
        values.ThrowIfInvalid();
        return applications.ListAsync(User.UserId(), new ApplicationListQuery(parsed, page, pageSize), cancellationToken);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ApplicationDetailDto> Get(Guid id, CancellationToken cancellationToken) =>
        WithVersion(await applications.GetAsync(User.UserId(), id, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ApplicationDetailDto> Update(Guid id, SaveApplicationRequest request, CancellationToken cancellationToken) =>
        WithVersion(await applications.UpdateAsync(User.UserId(), id, request, Request.ExpectedVersion(), cancellationToken));

    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ApplicationDetailDto> Submit(Guid id, CancellationToken cancellationToken) =>
        WithVersion(await applications.SubmitAsync(User.UserId(), id, Request.ExpectedVersion(), cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<ApplicationDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ApplicationDetailDto> Cancel(
        Guid id,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] CancelApplicationRequest? request,
        CancellationToken cancellationToken) =>
        WithVersion(await applications.CancelAsync(User.UserId(), id, request, Request.ExpectedVersion(), cancellationToken));

    /// <summary>Dépôt d'une pièce (multipart : <c>type</c>, <c>file</c>) — PDF, JPEG ou PNG, 5 Mo maximum.</summary>
    [HttpPost("{id:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ApiServiceCollectionExtensions.MaxRequestBodyBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ApiServiceCollectionExtensions.MaxRequestBodyBytes)]
    [ProducesResponseType<DocumentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentDto>> Upload(Guid id, [FromForm] UploadDocumentForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var values = new QueryValues();
        var type = values.Enum<DocumentType>(form.Type, "type");
        values.ThrowIfInvalid();

        await using var content = form.File?.OpenReadStream();
        var document = await documents.UploadAsync(User.UserId(), id, type, form.File?.FileName, content, cancellationToken);
        return Created($"/api/v1/applications/{id}/documents/{document.Id}", document);
    }

    [HttpDelete("{id:guid}/documents/{documentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteDocument(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        await documents.DeleteAsync(User.UserId(), id, documentId, cancellationToken);
        return NoContent();
    }

    private ApplicationDetailDto WithVersion(ApplicationDetailDto dto)
    {
        Response.SetVersionHeader(dto.Version);
        return dto;
    }
}

public sealed class UploadDocumentForm
{
    public string? Type { get; set; }

    public IFormFile? File { get; set; }
}

/// <summary>Téléchargement d'une pièce : client titulaire, ou analyste / administrateur pour une demande soumise.</summary>
[ApiController]
[Route("api/v1/applications/{id:guid}/documents/{documentId:guid}")]
[Authorize]
public sealed class DocumentDownloadsController(DocumentService documents) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, Guid documentId, CancellationToken cancellationToken)
    {
        var content = await documents.OpenAsync(User.ToActor(), id, documentId, cancellationToken);
        return File(content.Content, content.ContentType, content.FileName);
    }
}
