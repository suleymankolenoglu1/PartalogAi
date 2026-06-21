using FluentValidation;
using Katalogcu.Application.Features.ExternalSites.Commands;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ApproveCatalogExternalProductByUrl;

public sealed class ApproveCatalogExternalProductByUrlCommandValidator : AbstractValidator<ApproveCatalogExternalProductByUrlCommand>
{
    public ApproveCatalogExternalProductByUrlCommandValidator()
    {
        RuleFor(x => x.CatalogItemId).NotEmpty();
        RuleFor(x => x.ExternalSiteId).NotEmpty();
        RuleFor(x => x.ProductUrl)
            .NotEmpty()
            .Must(ExternalSiteUrlSecurityValidator.HasAllowedHttpScheme)
            .WithMessage("Geçerli bir HTTP(S) ürün adresi girin.")
            .MustAsync(ExternalSiteUrlSecurityValidator.IsSafeExternalUrlAsync)
            .WithMessage("İç ağ, localhost veya özel IP adresleri kullanılamaz.");
        RuleFor(x => x.ProductTitle).MaximumLength(512);
        RuleFor(x => x.ReviewNote).MaximumLength(1024);
    }
}
