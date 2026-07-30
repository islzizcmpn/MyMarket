namespace PcMarket.Payments.Click;

/// <summary>The fields Click posts (form-encoded) to the merchant callback for both Prepare and Complete.</summary>
public sealed record ClickCallbackRequest(
    string ClickTransId,
    string ServiceId,
    string ClickPaydocId,
    string MerchantTransId,
    string? MerchantPrepareId,
    string Amount,
    int Action,
    int Error,
    string? ErrorNote,
    string SignTime,
    string SignString);

/// <summary>Click Merchant API result codes. Returned verbatim in the <c>error</c> field.</summary>
public static class ClickError
{
    public const int Success = 0;
    public const int SignCheckFailed = -1;
    public const int InvalidAmount = -2;
    public const int ActionNotFound = -3;
    public const int AlreadyPaid = -4;
    public const int OrderNotFound = -5;
    public const int TransactionNotFound = -6;
    public const int TransactionCancelled = -9;
}

/// <summary>Click callback actions.</summary>
public static class ClickAction
{
    public const int Prepare = 0;
    public const int Complete = 1;
}
