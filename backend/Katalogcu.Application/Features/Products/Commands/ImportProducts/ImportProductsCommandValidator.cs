using FluentValidation;

namespace Katalogcu.Application.Features.Products.Commands.ImportProducts;

public sealed class ImportProductsCommandValidator : AbstractValidator<ImportProductsCommand>
{
    public ImportProductsCommandValidator()
    {
        RuleFor(x => x.Rows)
            .NotNull()
            .Must(rows => rows.Count > 0)
            .WithMessage("Dosyada okunabilir ürün bulunamadı.");
    }
}
