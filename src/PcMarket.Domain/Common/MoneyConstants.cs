namespace PcMarket.Domain.Common;

/// <summary>Money conventions. Amounts are stored as <c>decimal(18,2)</c>; never floating point.</summary>
public static class MoneyConstants
{
    /// <summary>ISO 4217 code for the Uzbek som — the store's single settlement currency.</summary>
    public const string DefaultCurrency = "UZS";

    public const int Precision = 18;
    public const int Scale = 2;
}
