using FluentValidation;

namespace Katalogcu.Application.Features.Folders.Commands.CreateFolder;

public sealed class CreateFolderCommandValidator : AbstractValidator<CreateFolderCommand>
{
    public CreateFolderCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Klasör adı zorunludur.");
    }
}
