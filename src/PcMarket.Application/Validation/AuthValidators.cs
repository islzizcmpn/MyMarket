using FluentValidation;
using PcMarket.Contracts.Auth;

namespace PcMarket.Application.Validation;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^\+998\d{9}$")
            .WithMessage("Phone must be in the format +998XXXXXXXXX.");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FullName).MaximumLength(200);
    }
}

public sealed class VerifyOtpRequestValidator : AbstractValidator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().Matches(@"^\+998\d{9}$");
        RuleFor(x => x.Code).NotEmpty().Length(6);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Phone).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}
