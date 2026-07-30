using System.Security.Cryptography;
using System.Text;

namespace PcMarket.Payments.Click;

/// <summary>Computes and verifies the MD5 <c>sign_string</c> Click sends with each callback. The Prepare and
/// Complete signatures differ only by the inclusion of <c>merchant_prepare_id</c>.</summary>
public static class ClickSignature
{
    public static string ForPrepare(ClickCallbackRequest r, string secretKey) =>
        Md5(r.ClickTransId + r.ServiceId + secretKey + r.MerchantTransId + r.Amount + r.Action + r.SignTime);

    public static string ForComplete(ClickCallbackRequest r, string secretKey) =>
        Md5(r.ClickTransId + r.ServiceId + secretKey + r.MerchantTransId + r.MerchantPrepareId + r.Amount + r.Action + r.SignTime);

    /// <summary>Verifies the request signature for its action. Returns false for unknown actions.</summary>
    public static bool IsValid(ClickCallbackRequest r, string secretKey)
    {
        var expected = r.Action switch
        {
            ClickAction.Prepare => ForPrepare(r, secretKey),
            ClickAction.Complete => ForComplete(r, secretKey),
            _ => null
        };

        return expected is not null
               && CryptographicOperations.FixedTimeEquals(
                   Encoding.ASCII.GetBytes(expected),
                   Encoding.ASCII.GetBytes(r.SignString.ToLowerInvariant()));
    }

    private static string Md5(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }
}
