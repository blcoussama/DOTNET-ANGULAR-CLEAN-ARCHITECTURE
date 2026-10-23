using CreditFlow.Application.Auth;
using CreditFlow.Application.Common;
using CreditFlow.Domain.Common;
using FluentValidation;

namespace CreditFlow.Application.Users;

public sealed class UserListQueryValidator : AbstractValidator<UserListQuery>
{
    public UserListQueryValidator()
    {
        RuleFor(x => x.Page).ValidPage();
        RuleFor(x => x.PageSize).ValidPageSize();
        RuleFor(x => x.Role).Must(Roles.IsValid).When(x => x.Role is not null).WithMessage("Rôle invalide (Client, Analyst ou Admin).");
        RuleFor(x => x.Search).MaximumLength(100).WithMessage("La recherche est limitée à 100 caractères.");
    }
}

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.FirstName).PersonName("prénom");
        RuleFor(x => x.LastName).PersonName("nom");
        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Le rôle est obligatoire.")
            .Must(r => r is Roles.Analyst or Roles.Admin).WithMessage("Le rôle doit être Analyst ou Admin.");
        RuleFor(x => x.TemporaryPassword).StrongPassword();
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x).Must(x => x.Role is not null || x.IsActive is not null)
            .OverridePropertyName("request").WithMessage("Indiquez au moins un champ à modifier (role, isActive).");
        RuleFor(x => x.Role)
            .Must(r => r is Roles.Analyst or Roles.Admin).When(x => x.Role is not null)
            .WithMessage("Le rôle doit être Analyst ou Admin.");
    }
}
