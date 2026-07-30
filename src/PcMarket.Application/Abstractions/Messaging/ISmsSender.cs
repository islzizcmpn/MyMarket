namespace PcMarket.Application.Abstractions.Messaging;

/// <summary>Sends SMS messages (OTP codes, notifications). A no-op/dev implementation logs instead of
/// sending until a real UZ provider (e.g. Eskiz/Play Mobile) is configured.</summary>
public interface ISmsSender
{
    Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}
