using FluentValidation;
using PcMarket.Contracts.Orders;
using PcMarket.Contracts.Payments;

namespace PcMarket.Application.Validation;

public sealed class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(x => x.PaymentMethod).IsInEnum();
        RuleFor(x => x.DeliveryType).IsInEnum();

        // A delivery address (saved or inline) is required for courier delivery.
        RuleFor(x => x)
            .Must(x => x.DeliveryType != DeliveryType.Courier || x.AddressId is not null || x.Address is not null)
            .WithMessage("A delivery address is required for courier delivery.")
            .WithName("Address");

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Region).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Address!.City).NotEmpty().MaximumLength(120);
            RuleFor(x => x.Address!.Street).NotEmpty().MaximumLength(300);
            RuleFor(x => x.Address!.Details).MaximumLength(500);
        });
    }
}

public sealed class PaymentInitiateRequestValidator : AbstractValidator<PaymentInitiateRequest>
{
    public PaymentInitiateRequestValidator() => RuleFor(x => x.OrderId).NotEmpty();
}
