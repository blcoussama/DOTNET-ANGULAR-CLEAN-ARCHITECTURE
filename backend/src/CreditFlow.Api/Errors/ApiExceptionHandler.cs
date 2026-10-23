using CreditFlow.Application.Common;
using CreditFlow.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CreditFlow.Api.Errors;

/// <summary>
/// Traduit les exceptions en ProblemDetails (RFC 7807). Aucune information interne (pile, message technique)
/// n'est renvoyée pour une erreur inattendue.
/// </summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var problem = exception switch
        {
            FluentValidation.ValidationException validation => Validation(
                validation.Errors.GroupBy(e => ValidationErrorKeys.Normalize(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray()),
                validation.Errors.Select(e => e.ErrorMessage).FirstOrDefault()),
            DomainValidationException domain => Validation(
                new Dictionary<string, string[]> { [ValidationErrorKeys.Normalize(domain.Field)] = [domain.Message] },
                domain.Message),
            NotFoundException notFound => Problem(StatusCodes.Status404NotFound, notFound.Message),
            ForbiddenException forbidden => Problem(StatusCodes.Status403Forbidden, forbidden.Message),
            ConflictException conflict => Problem(StatusCodes.Status409Conflict, conflict.Message, conflict.Code),
            DomainConflictException conflict => Problem(StatusCodes.Status409Conflict, conflict.Message, conflict.Code),
            DbUpdateConcurrencyException => Problem(
                StatusCodes.Status409Conflict,
                "La ressource a été modifiée par une autre opération. Rechargez-la avant de réessayer.",
                "concurrency_conflict"),
            AuthenticationFailedException auth => Problem(StatusCodes.Status401Unauthorized, auth.Message, auth.Code),
            AccountLockedException locked => Problem(StatusCodes.Status423Locked, locked.Message, locked.Code),
            BadHttpRequestException badRequest => Problem(
                badRequest.StatusCode,
                badRequest.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? "Le contenu envoyé dépasse la taille autorisée."
                    : "La requête est mal formée."),
            InvalidDataException => Problem(StatusCodes.Status413PayloadTooLarge, "Le contenu envoyé dépasse la taille autorisée."),
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Erreur non gérée sur {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            problem = Problem(StatusCodes.Status500InternalServerError, "Une erreur interne est survenue. Réessayez plus tard.");
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ValidationProblemDetails Validation(Dictionary<string, string[]> errors, string? detail) =>
        new(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = errors.Count > 1 ? "Certains champs sont invalides." : detail,
        };

    private static ProblemDetails Problem(int status, string detail, string? code = null)
    {
        var problem = new ProblemDetails { Status = status, Detail = detail };
        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return problem;
    }
}
