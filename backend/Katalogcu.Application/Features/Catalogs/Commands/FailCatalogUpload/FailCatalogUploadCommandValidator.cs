using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.FailCatalogUpload;

public sealed class FailCatalogUploadCommandValidator : AbstractValidator<FailCatalogUploadCommand>
{
    public FailCatalogUploadCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
