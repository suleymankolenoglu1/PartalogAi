using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.CompleteCatalogUpload;

public sealed class CompleteCatalogUploadCommandValidator : AbstractValidator<CompleteCatalogUploadCommand>
{
    public CompleteCatalogUploadCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
