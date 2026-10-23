using FluentValidation;
using FluentValidation.Results;

namespace CreditFlow.Application.Common;

public static class ValidationExtensions
{
    /// <summary>Valide et lève <see cref="ValidationException"/> (traduite en 400 avec <c>errors</c> par champ).</summary>
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var result = await validator.ValidateAsync(instance, cancellationToken);
        if (!result.IsValid)
        {
            throw new ValidationException(result.Errors);
        }
    }

    public static ValidationException ValidationError(string field, string message) =>
        new(message, [new ValidationFailure(field, message)]);

    public static ValidationException ValidationErrors(IEnumerable<(string Field, string Message)> errors)
    {
        var failures = errors.Select(e => new ValidationFailure(e.Field, e.Message)).ToList();
        return new ValidationException(failures.Count == 1 ? failures[0].ErrorMessage : "Plusieurs champs sont invalides.", failures);
    }

    public static IRuleBuilderOptions<T, int> ValidPage<T>(this IRuleBuilder<T, int> rule) =>
        rule.GreaterThanOrEqualTo(1).WithMessage("Le numéro de page doit être supérieur ou égal à 1.");

    public static IRuleBuilderOptions<T, int> ValidPageSize<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, Paging.MaxPageSize).WithMessage($"La taille de page doit être comprise entre 1 et {Paging.MaxPageSize}.");
}
