using FluentValidation;

namespace Katalogcu.Application.Features.Orders.Commands.CreateOrder;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .MaximumLength(128)
            .WithMessage("Idempotency key en fazla 128 karakter olabilir.");

        RuleFor(x => x)
            .Must(x =>
                (x.AuthenticatedUserId.HasValue && x.AuthenticatedUserId.Value != Guid.Empty) ||
                (x.PublicUserId.HasValue && x.PublicUserId.Value != Guid.Empty))
            .WithMessage("Sipariş kapsamı belirlenemedi. Geçerli kullanıcı veya public token gerekli.");

        RuleFor(x => x.CustomerName)
            .NotEmpty()
            .WithMessage("Müşteri adı zorunludur.");

        RuleFor(x => x.CustomerPhone)
            .NotEmpty()
            .WithMessage("Müşteri telefonu zorunludur.");

        RuleFor(x => x.DeliveryAddress)
            .NotEmpty()
            .WithMessage("Teslimat adresi zorunludur.");

        RuleFor(x => x.DeliveryCity)
            .NotEmpty()
            .WithMessage("Teslimat şehri zorunludur.");

        RuleFor(x => x.Items)
            .NotNull()
            .Must(items => items.Count > 0)
            .WithMessage("Sepet boş, sipariş oluşturulamaz.");
    }
}
