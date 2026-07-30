using FluentValidation;
using PcMarket.Contracts.Cart;
using PcMarket.Contracts.Users;

namespace PcMarket.Application.Validation;

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ProductVariantId).NotEmpty();
        RuleFor(x => x.Qty).GreaterThanOrEqualTo(1);
    }
}

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator() => RuleFor(x => x.Qty).GreaterThanOrEqualTo(0);
}

public sealed class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    public CreateAddressRequestValidator()
    {
        RuleFor(x => x.Region).NotEmpty().MaximumLength(120);
        RuleFor(x => x.City).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Details).MaximumLength(500);
    }
}

public sealed class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    public UpdateAddressRequestValidator()
    {
        RuleFor(x => x.Region).NotEmpty().MaximumLength(120);
        RuleFor(x => x.City).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Street).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Details).MaximumLength(500);
    }
}

public sealed class RegisterDeviceTokenRequestValidator : AbstractValidator<RegisterDeviceTokenRequest>
{
    public RegisterDeviceTokenRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(400);
        RuleFor(x => x.Platform).IsInEnum();
    }
}
