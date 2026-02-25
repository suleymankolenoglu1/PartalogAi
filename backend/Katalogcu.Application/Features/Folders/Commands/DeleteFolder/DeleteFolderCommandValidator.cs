using FluentValidation;

namespace Katalogcu.Application.Features.Folders.Commands.DeleteFolder;

public sealed class DeleteFolderCommandValidator : AbstractValidator<DeleteFolderCommand>
{
    public DeleteFolderCommandValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçerli bir klasör seçiniz.");
    }
}
