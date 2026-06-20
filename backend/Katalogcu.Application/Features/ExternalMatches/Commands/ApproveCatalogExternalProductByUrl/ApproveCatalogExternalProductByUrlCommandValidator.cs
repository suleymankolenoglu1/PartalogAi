using FluentValidation;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ApproveCatalogExternalProductByUrl;

public sealed class ApproveCatalogExternalProductByUrlCommandValidator : AbstractValidator<ApproveCatalogExternalProductByUrlCommand>
{
    public ApproveCatalogExternalProductByUrlCommandValidator()
    {
        RuleFor(x => x.CatalogItemId).NotEmpty();
        RuleFor(x => x.ExternalSiteId).NotEmpty();
        RuleFor(x => x.ProductUrl)
            .NotEmpty()
            .Must(BeValidHttpUrl)
            .WithMessage("Geçerli bir ürün adresi girin.");
        RuleFor(x => x.ProductTitle).MaximumLength(512);
        RuleFor(x => x.ReviewNote).MaximumLength(1024);
    }

    private static bool BeValidHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
