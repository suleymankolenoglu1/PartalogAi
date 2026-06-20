using FluentValidation;

namespace Katalogcu.Application.Features.ExternalSites.Commands.ImportExternalSiteProductsFromFile;

public sealed class ImportExternalSiteProductsFromFileCommandValidator : AbstractValidator<ImportExternalSiteProductsFromFileCommand>
{
    public ImportExternalSiteProductsFromFileCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.File.Length).GreaterThan(0);
        RuleFor(x => x.FileType)
            .Must(x => string.IsNullOrWhiteSpace(x)
                       || x.Equals("csv", StringComparison.OrdinalIgnoreCase)
                       || x.Equals("xlsx", StringComparison.OrdinalIgnoreCase)
                       || x.Equals("xml", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Desteklenen fileType değerleri: csv, xlsx, xml.");
    }
}
