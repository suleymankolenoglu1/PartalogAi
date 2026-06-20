using FluentValidation;

namespace Katalogcu.Application.Features.Compatibility.Commands.CreatePartCompatibilityRule;

public sealed class CreatePartCompatibilityRuleCommandValidator : AbstractValidator<CreatePartCompatibilityRuleCommand>
{
    private static readonly HashSet<string> AllowedLevels = ["Exact", "Likely", "SameAssembly", "Unknown", "Incompatible"];

    public CreatePartCompatibilityRuleCommandValidator()
    {
        RuleFor(x => x.CatalogItemId).NotEmpty();
        RuleFor(x => x.MachineModelId).NotEmpty();
        RuleFor(x => x.CompatibilityLevel)
            .Must(level => AllowedLevels.Contains(level))
            .WithMessage("Uyumluluk seviyesi geçersiz.");
        RuleFor(x => x.SourceType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Confidence).InclusiveBetween(0m, 1m);
        RuleFor(x => x.Notes).MaximumLength(1024);
    }
}
