using System.Net;

namespace PcMarket.Bot.Presentation;

/// <summary>Guards URL buttons against addresses Telegram will not accept.
///
/// Telegram validates every <c>InlineKeyboardButton.WithUrl</c> server-side and rejects the <em>entire</em>
/// message with <c>400 Bad Request: ... is invalid: Wrong HTTP URL</c> if any one of them is unreachable
/// from the public internet. A single bad button therefore takes down the whole card it was attached to,
/// not just the button — which is exactly how a `localhost` storefront URL silently broke every product
/// detail view (docs/issues/bot-product-click-no-cart/journal.md).
///
/// So a URL that cannot possibly work is dropped at build time and the surrounding message still renders.</summary>
public static class PublicUrl
{
    /// <summary>True when <paramref name="url"/> is an absolute http(s) address Telegram has a chance of
    /// reaching. Deliberately conservative: it rejects anything privately routable rather than trying to
    /// prove reachability, because the cost of a false negative (one missing button) is far lower than the
    /// cost of a false positive (the whole message rejected).</summary>
    public static bool IsReachableByTelegram(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        // Covers "localhost", 127.0.0.0/8 and ::1.
        if (uri.IsLoopback)
        {
            return false;
        }

        var host = uri.Host;

        // Compose service names ("api", "nginx") and the *.localhost convention the dev stack routes on.
        if (!host.Contains('.') || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsPrivateAddress(host);
    }

    private static bool IsPrivateAddress(string host)
    {
        if (!IPAddress.TryParse(host, out var ip))
        {
            return false;
        }

        var octets = ip.GetAddressBytes();
        if (octets.Length != 4)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
        }

        return octets[0] switch
        {
            10 => true,
            172 => octets[1] >= 16 && octets[1] <= 31,
            192 => octets[1] == 168,
            169 => octets[1] == 254,
            _ => false
        };
    }
}
