using FluentValidation;

namespace Katalogcu.Application.Features.Products.Commands.AdjustStock;

public sealed class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Ürün seçimi zorunludur.");

        RuleFor(x => x.DeltaQuantity)
            .NotEqual(0).WithMessage("Değişim miktarı 0 olamaz.");

        RuleFor(x => x.Reason)
            .MaximumLength(300).WithMessage("Açıklama en fazla 300 karakter olabilir.");
    }
}
