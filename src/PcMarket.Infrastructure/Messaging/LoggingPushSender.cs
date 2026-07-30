using Microsoft.Extensions.Logging;
using PcMarket.Application.Abstractions.Messaging;

namespace PcMarket.Infrastructure.Messaging;

/// <summary>Stands in for a live FCM sender until a Firebase project and service-account credentials exist.
/// Logs what would have been delivered and reports success, so an unconfigured environment never fails — or
/// retries — a notification. Swap the registration in <c>DependencyInjection</c> for the real sender.</summary>
public sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public bool IsConfigured => false;

    public Task<bool> SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[Push:noop] → token {TokenPrefix}…: {Title} — {Body}",
            message.Token.Length > 12 ? message.Token[..12] : message.Token,
            message.Title,
            message.Body);

        return Task.FromResult(true);
    }
}
