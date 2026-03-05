using FluentValidation;

namespace Katalogcu.Application.Features.Folders.Queries.GetPublicFoldersByUser;

public sealed class GetPublicFoldersByUserQueryValidator : AbstractValidator<GetPublicFoldersByUserQuery>
{
    public GetPublicFoldersByUserQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId zorunludur.");
    }
}

