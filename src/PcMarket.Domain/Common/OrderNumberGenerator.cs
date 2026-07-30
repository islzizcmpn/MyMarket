using System.Security.Cryptography;

namespace PcMarket.Domain.Common;

/// <summary>Produces human-readable, practically-unique order numbers of the form
/// <c>ORD-yyMMdd-XXXXXXXX</c>. The unique index on <c>Order.Number</c> is the final guard.</summary>
public static class OrderNumberGenerator
{
    public static string Generate(DateTimeOffset? now = null)
    {
        var stamp = (now ?? DateTimeOffset.UtcNow).ToString("yyMMdd");
        var suffix = RandomNumberGenerator.GetHexString(8, lowercase: false);
        return $"ORD-{stamp}-{suffix}";
    }
}
