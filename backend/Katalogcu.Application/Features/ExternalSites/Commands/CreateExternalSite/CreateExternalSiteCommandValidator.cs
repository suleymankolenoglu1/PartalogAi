using FluentValidation;
using Katalogcu.Application.Features.ExternalSites.Commands;

namespace Katalogcu.Application.Features.ExternalSites.Commands.CreateExternalSite;

public sealed class CreateExternalSiteCommandValidator : AbstractValidator<CreateExternalSiteCommand>
{
    public CreateExternalSiteCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUrl)
            .NotEmpty()
            .MaximumLength(500)
            .Must(ExternalSiteUrlSecurityValidator.HasAllowedHttpScheme)
            .WithMessage("Sadece http:// ve https:// ile başlayan geçerli bir site adresi girin.")
            .MustAsync(async (baseUrl, cancellationToken) =>
                await ExternalSiteUrlSecurityValidator.IsSafeExternalUrlAsync(baseUrl, cancellationToken))
            .WithMessage("İç ağ, localhost veya özel IP adresleri kullanılamaz.");
        RuleFor(x => x.PreferredCrawlMode)
            .NotEmpty()
            .Must(x => x is "auto" or "fetch_only" or "fetch_browser" or "manual_import")
            .WithMessage("Geçersiz tarama modu.");
    }
}
