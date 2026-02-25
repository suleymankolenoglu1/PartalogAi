using FluentValidation;

namespace Katalogcu.Application.Features.Products.Commands.ImportStock;

public sealed class ImportStockCommandValidator : AbstractValidator<ImportStockCommand>
{
    public ImportStockCommandValidator()
    {
        RuleFor(x => x.Rows)
            .NotNull()
            .Must(rows => rows.Count > 0)
            .WithMessage("Dosyada işlenecek satır bulunamadı.");

        RuleFor(x => x.Mode)
            .NotEmpty()
            .Must(IsSupportedMode)
            .WithMessage("Geçersiz import modu. Desteklenen: update_only, upsert.");

        RuleFor(x => x)
            .Must(x => !IsUpsert(x.Mode) || (x.CatalogId.HasValue && x.CatalogId.Value != Guid.Empty))
            .WithMessage("Upsert modunda yeni ürün oluşturmak için catalogId zorunludur.");
    }

    private static bool IsSupportedMode(string? mode)
    {
        return string.Equals(mode, "update_only", StringComparison.OrdinalIgnoreCase)
               || string.Equals(mode, "upsert", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUpsert(string? mode)
    {
        return string.Equals(mode, "upsert", StringComparison.OrdinalIgnoreCase);
    }
}
