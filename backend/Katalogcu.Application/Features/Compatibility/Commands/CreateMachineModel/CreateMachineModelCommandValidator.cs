using FluentValidation;

namespace Katalogcu.Application.Features.Compatibility.Commands.CreateMachineModel;

public sealed class CreateMachineModelCommandValidator : AbstractValidator<CreateMachineModelCommand>
{
    public CreateMachineModelCommandValidator()
    {
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Variant).MaximumLength(128);
        RuleFor(x => x.MachineGroup).MaximumLength(128);
    }
}
