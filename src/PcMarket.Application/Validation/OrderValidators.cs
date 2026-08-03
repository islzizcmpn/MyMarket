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
            RuleFor(x => x.Address!.Region).MaximumLength(120);
            RuleFor(x => x.Address!.City).MaximumLength(120);
            RuleFor(x => x.Address!.Street).NotEmpty().MaximumLength(300);
            RuleFor(x => x.Address!.Details).MaximumLength(500);

            // An order has to say where it is going, in one of the two ways a client can express that: a
            // written region and city (the web checkout), or a map pin (the Telegram bot, which asks for a
            // pin and a flat number instead). Requiring both would reject either client.
            RuleFor(x => x.Address!)
                .Must(address => HasCoordinates(address) || (!string.IsNullOrWhiteSpace(address.Region) && !string.IsNullOrWhiteSpace(address.City)))
                .WithMessage("An address needs either a region and city or a map location.")
                .WithName("Address");

            // A single coordinate locates nothing, so the pair travels together or not at all.
            RuleFor(x => x.Address!)
                .Must(address => address.Latitude is null == address.Longitude is null)
                .WithMessage("Latitude and longitude must be supplied together.")
                .WithName("Address");

            RuleFor(x => x.Address!.Latitude).InclusiveBetween(-90, 90).When(x => x.Address!.Latitude is not null);
            RuleFor(x => x.Address!.Longitude).InclusiveBetween(-180, 180).When(x => x.Address!.Longitude is not null);
        });
    }

    private static bool HasCoordinates(ShippingAddressDto address) =>
        address.Latitude is not null && address.Longitude is not null;
}

public sealed class PaymentInitiateRequestValidator : AbstractValidator<PaymentInitiateRequest>
{
    public PaymentInitiateRequestValidator() => RuleFor(x => x.OrderId).NotEmpty();
}
