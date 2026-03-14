using FluentValidation;

namespace Katalogcu.Application.Features.Products.Commands.ApplyErpInventoryWebhook;

public sealed class ApplyErpInventoryWebhookCommandValidator : AbstractValidator<ApplyErpInventoryWebhookCommand>
{
    public ApplyErpInventoryWebhookCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Source).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Items).NotEmpty();

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.PartCode).MaximumLength(128);
            item.RuleFor(x => x.ExternalProductId).MaximumLength(128);
            item.RuleFor(x => x.StockQuantity)
                .GreaterThanOrEqualTo(0)
                .When(x => x.StockQuantity.HasValue);
            item.RuleFor(x => x)
                .Must(x =>
                    x.ProductId.HasValue ||
                    !string.IsNullOrWhiteSpace(x.PartCode) ||
                    !string.IsNullOrWhiteSpace(x.ExternalProductId))
                .WithMessage("Her ERP stok kaydında productId, partCode veya externalProductId olmalıdır.");
        });
    }
}
