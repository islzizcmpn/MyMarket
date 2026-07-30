using Microsoft.Extensions.Logging;
using PcMarket.Application.Abstractions.Messaging;

namespace PcMarket.Infrastructure.Messaging;

/// <summary>Development <see cref="ISmsSender"/> that logs messages instead of sending them. Swap for a
/// real UZ provider (Eskiz/Play Mobile) implementation when credentials are available.</summary>
public sealed class NoOpSmsSender(ILogger<NoOpSmsSender> logger) : ISmsSender
{
    public Task SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[DEV SMS] To {PhoneNumber}: {Message}", phoneNumber, message);
        return Task.CompletedTask;
    }
}
