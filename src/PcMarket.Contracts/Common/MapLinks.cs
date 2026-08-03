using System.Globalization;

namespace PcMarket.Contracts.Common;

/// <summary>Turns a pair of coordinates into map links a courier can open on any phone. Deliberately plain
/// URLs against the public map sites: no API key, no billing account, and nothing to break when a key
/// expires. Both providers are offered because Yandex Maps has the better address and building coverage in
/// Uzbekistan, while Google is what most people already have installed.</summary>
public static class MapLinks
{
    /// <summary>Coordinates are formatted invariantly — a comma decimal separator would silently produce a
    /// URL pointing somewhere else entirely.</summary>
    public static string Google(double latitude, double longitude) =>
        $"https://www.google.com/maps/search/?api=1&query={Format(latitude)},{Format(longitude)}";

    public static string Yandex(double latitude, double longitude) =>
        $"https://yandex.com/maps/?pt={Format(longitude)},{Format(latitude)}&z=18&l=map";

    /// <summary>Human-readable pin, e.g. <c>41.311081, 69.240562</c>.</summary>
    public static string Coordinates(double latitude, double longitude) =>
        $"{Format(latitude)}, {Format(longitude)}";

    private static string Format(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);
}
