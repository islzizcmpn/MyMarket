using PcMarket.Domain.Enums;
using PcMarket.Domain.Payments;

namespace PcMarket.Payments.Payme;

/// <summary>Payme JSON-RPC method names.</summary>
public static class PaymeMethod
{
    public const string CheckPerformTransaction = "CheckPerformTransaction";
    public const string CreateTransaction = "CreateTransaction";
    public const string PerformTransaction = "PerformTransaction";
    public const string CancelTransaction = "CancelTransaction";
    public const string CheckTransaction = "CheckTransaction";
    public const string GetStatement = "GetStatement";
}

/// <summary>Payme Merchant API error codes.</summary>
public static class PaymeError
{
    public const int InsufficientPrivilege = -32504;
    public const int MethodNotFound = -32601;
    public const int InvalidAmount = -31001;
    public const int TransactionNotFound = -31003;
    public const int UnableToPerform = -31008;
    public const int UnableToCancel = -31007;
    public const int OrderNotFound = -31050;
}

/// <summary>Payme transaction state codes carried in JSON-RPC results.</summary>
public static class PaymeState
{
    public const int Created = 1;
    public const int Performed = 2;
    public const int CancelledDuringCreated = -1;
    public const int CancelledAfterPerformed = -2;

    public static int Of(PaymentTransaction txn) => txn.State switch
    {
        PaymentTransactionState.Pending => Created,
        PaymentTransactionState.Performed => Performed,
        PaymentTransactionState.Cancelled => txn.PerformedAt is not null ? CancelledAfterPerformed : CancelledDuringCreated,
        _ => 0
    };
}

/// <summary>The order-identifying account field passed in Payme <c>params.account</c>.</summary>
public static class PaymeAccount
{
    public const string OrderIdField = "order_id";
}

/// <summary>Converts between the store's decimal som and Payme's integer tiyin (1 som = 100 tiyin).</summary>
public static class PaymeAmount
{
    public static long ToTiyin(decimal som) => (long)decimal.Round(som * 100m, 0);

    public static bool Matches(long tiyin, decimal som) => tiyin == ToTiyin(som);
}

/// <summary>Unix-millisecond timestamps, the format Payme expects for transaction times.</summary>
public static class PaymeTime
{
    public static long ToUnixMs(DateTimeOffset? value) => value?.ToUnixTimeMilliseconds() ?? 0;
}
