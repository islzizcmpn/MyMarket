namespace PcMarket.Payments.Configuration;

/// <summary>Root payments configuration, bound from the <c>Payments</c> section. Each rail carries an
/// <c>Enabled</c> feature flag plus its gateway credentials.</summary>
public sealed class PaymentsSettings
{
    /// <summary>How long an unpaid online order may sit in AwaitingPayment before auto-cancel.</summary>
    public int UnpaidOrderTimeoutMinutes { get; set; } = 30;

    public ClickSettings Click { get; set; } = new();
    public PaymeSettings Payme { get; set; } = new();
    public ProviderToggle Cash { get; set; } = new();
    public ClickSettings Uzcard { get; set; } = new();
    public ClickSettings Humo { get; set; } = new();
}

/// <summary>Minimal on/off flag for rails with no extra credentials (e.g. cash on delivery).</summary>
public class ProviderToggle
{
    public bool Enabled { get; set; } = true;
}

/// <summary>Click Merchant API settings. Uzcard/Humo ride the same rails and reuse this shape.</summary>
public sealed class ClickSettings : ProviderToggle
{
    public string ServiceId { get; set; } = string.Empty;
    public string MerchantId { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Base of the hosted Click checkout page.</summary>
    public string CheckoutUrl { get; set; } = "https://my.click.uz/services/pay";

    /// <summary>Where Click returns the customer after payment.</summary>
    public string ReturnUrl { get; set; } = string.Empty;
}

/// <summary>Payme Merchant API (JSON-RPC) settings.</summary>
public sealed class PaymeSettings : ProviderToggle
{
    public string MerchantId { get; set; } = string.Empty;

    /// <summary>The merchant key used both to authenticate inbound JSON-RPC (Basic header) and, historically,
    /// to sign the checkout link.</summary>
    public string MerchantKey { get; set; } = string.Empty;

    /// <summary>Base of the hosted Payme checkout page.</summary>
    public string CheckoutUrl { get; set; } = "https://checkout.paycom.uz";
}
