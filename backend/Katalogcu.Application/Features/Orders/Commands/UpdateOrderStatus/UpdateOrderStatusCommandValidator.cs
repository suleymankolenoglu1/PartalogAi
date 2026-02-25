using FluentValidation;

namespace Katalogcu.Application.Features.Orders.Commands.UpdateOrderStatus;

public sealed class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçerli bir sipariş seçiniz.");

        RuleFor(x => x.Note)
            .MaximumLength(512)
            .WithMessage("Durum notu en fazla 512 karakter olabilir.");
    }
}
