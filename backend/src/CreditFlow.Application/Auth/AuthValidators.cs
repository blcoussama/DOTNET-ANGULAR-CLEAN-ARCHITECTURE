using FluentValidation;

namespace CreditFlow.Application.Auth;

internal static class AuthRules
{
    public static IRuleBuilderOptions<T, string?> ValidEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithMessage("L'adresse e-mail est obligatoire.")
            .MaximumLength(256).WithMessage("L'adresse e-mail est limitée à 256 caractères.")
            .EmailAddress().WithMessage("Adresse e-mail invalide.");

    public static IRuleBuilderOptions<T, string?> StrongPassword<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().WithMessage("Le mot de passe est obligatoire.")
            .Length(PasswordPolicy.MinLength, PasswordPolicy.MaxLength).WithMessage($"Le mot de passe doit contenir entre {PasswordPolicy.MinLength} et {PasswordPolicy.MaxLength} caractères.")
            .Matches("[A-Z]").WithMessage("Le mot de passe doit contenir au moins une majuscule.")
            .Matches("[a-z]").WithMessage("Le mot de passe doit contenir au moins une minuscule.")
            .Matches("[0-9]").WithMessage("Le mot de passe doit contenir au moins un chiffre.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Le mot de passe doit contenir au moins un caractère spécial.");

    public static IRuleBuilderOptions<T, string?> PersonName<T>(this IRuleBuilder<T, string?> rule, string label) =>
        rule.NotEmpty().WithMessage($"Le {label} est obligatoire.")
            .MaximumLength(100).WithMessage($"Le {label} est limité à 100 caractères.");
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.Password).StrongPassword();
        RuleFor(x => x.FirstName).PersonName("prénom");
        RuleFor(x => x.LastName).PersonName("nom");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("L'adresse e-mail est obligatoire.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Le mot de passe est obligatoire.");
    }
}
