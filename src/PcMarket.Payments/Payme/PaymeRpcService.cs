using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering;
using PcMarket.Domain.Payments;
using PcMarket.Payments.Configuration;

namespace PcMarket.Payments.Payme;

/// <summary>Implements the Payme (Paycom) Merchant API over JSON-RPC. Every method is idempotent and keyed
/// by Payme's transaction <c>id</c>, mapped onto the <see cref="PaymentTransaction"/> ledger, so replayed
/// calls never double-apply. Inbound calls are authenticated by the merchant-key Basic header.</summary>
public sealed class PaymeRpcService(IApplicationDbContext db, IOptions<PaymentsSettings> settings)
{
    /// <summary>Dispatches a JSON-RPC request, returning the JSON-RPC response object to serialize.</summary>
    public async Task<object> HandleAsync(JsonElement rpc, string? authorizationHeader, CancellationToken cancellationToken = default)
    {
        var id = rpc.TryGetProperty("id", out var idEl) ? ExtractId(idEl) : null;

        if (!IsAuthorized(authorizationHeader))
        {
            return Error(id, PaymeError.InsufficientPrivilege, "Insufficient privilege to perform this operation.");
        }

        var method = rpc.TryGetProperty("method", out var methodEl) ? methodEl.GetString() : null;
        var parameters = rpc.TryGetProperty("params", out var paramsEl) ? paramsEl : default;

        return method switch
        {
            PaymeMethod.CheckPerformTransaction => await CheckPerformAsync(id, parameters, cancellationToken),
            PaymeMethod.CreateTransaction => await CreateAsync(id, parameters, cancellationToken),
            PaymeMethod.PerformTransaction => await PerformAsync(id, parameters, cancellationToken),
            PaymeMethod.CancelTransaction => await CancelAsync(id, parameters, cancellationToken),
            PaymeMethod.CheckTransaction => await CheckAsync(id, parameters, cancellationToken),
            PaymeMethod.GetStatement => await StatementAsync(id, parameters, cancellationToken),
            _ => Error(id, PaymeError.MethodNotFound, "Method not found.")
        };
    }

    private async Task<object> CheckPerformAsync(object? id, JsonElement p, CancellationToken cancellationToken)
    {
        var order = await FindOrderAsync(p, cancellationToken);
        if (order is null)
        {
            return AccountError(id);
        }

        if (!PaymeAmount.Matches(GetAmount(p), order.Total))
        {
            return Error(id, PaymeError.InvalidAmount, "Incorrect amount.");
        }

        if (order.Status != OrderStatus.AwaitingPayment)
        {
            return Error(id, PaymeError.UnableToPerform, "Order is not awaiting payment.");
        }

        return Result(id, new Dictionary<string, object?> { ["allow"] = true });
    }

    private async Task<object> CreateAsync(object? id, JsonElement p, CancellationToken cancellationToken)
    {
        var paymeId = GetTransactionId(p);
        var existing = await FindByPaymeIdAsync(paymeId, cancellationToken);
        if (existing is not null)
        {
            return existing.State == PaymentTransactionState.Pending
                ? CreateResult(id, existing)
                : Error(id, PaymeError.UnableToPerform, "Transaction is in an invalid state.");
        }

        var order = await FindOrderAsync(p, cancellationToken);
        if (order is null)
        {
            return AccountError(id);
        }

        if (!PaymeAmount.Matches(GetAmount(p), order.Total))
        {
            return Error(id, PaymeError.InvalidAmount, "Incorrect amount.");
        }

        if (order.Status != OrderStatus.AwaitingPayment)
        {
            return Error(id, PaymeError.UnableToPerform, "Order is not awaiting payment.");
        }

        // Only one active Payme transaction per order.
        var openForOrder = await db.PaymentTransactions.FirstOrDefaultAsync(
            t => t.OrderId == order.Id && t.Provider == PaymentProvider.Payme
                 && t.State == PaymentTransactionState.Pending && t.ProviderTxnId != null,
            cancellationToken);
        if (openForOrder is not null)
        {
            return Error(id, PaymeError.UnableToPerform, "Another transaction is already in progress for this order.");
        }

        var txn = await db.PaymentTransactions.FirstOrDefaultAsync(
                      t => t.OrderId == order.Id && t.Provider == PaymentProvider.Payme
                           && t.State == PaymentTransactionState.Created, cancellationToken)
                  ?? Track(new PaymentTransaction { OrderId = order.Id, Provider = PaymentProvider.Payme });

        txn.ProviderTxnId = paymeId;
        txn.State = PaymentTransactionState.Pending;
        txn.Amount = order.Total;
        txn.CreatedAt = DateTimeOffset.UtcNow;
        txn.RawPayload = p.GetRawText();
        await db.SaveChangesAsync(cancellationToken);

        return CreateResult(id, txn);
    }

    private async Task<object> PerformAsync(object? id, JsonElement p, CancellationToken cancellationToken)
    {
        var txn = await FindByPaymeIdAsync(GetTransactionId(p), cancellationToken);
        if (txn is null)
        {
            return Error(id, PaymeError.TransactionNotFound, "Transaction not found.");
        }

        if (txn.State == PaymentTransactionState.Pending)
        {
            txn.State = PaymentTransactionState.Performed;
            txn.PerformedAt = DateTimeOffset.UtcNow;

            var order = await db.Orders.Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.Id == txn.OrderId, cancellationToken);
            if (order is { Status: OrderStatus.AwaitingPayment })
            {
                order.TransitionTo(OrderStatus.Paid, "gateway:payme");
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return txn.State == PaymentTransactionState.Performed
            ? PerformResult(id, txn)
            : Error(id, PaymeError.UnableToPerform, "Transaction cannot be performed.");
    }

    private async Task<object> CancelAsync(object? id, JsonElement p, CancellationToken cancellationToken)
    {
        var txn = await FindByPaymeIdAsync(GetTransactionId(p), cancellationToken);
        if (txn is null)
        {
            return Error(id, PaymeError.TransactionNotFound, "Transaction not found.");
        }

        if (txn.State != PaymentTransactionState.Cancelled)
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.Id == txn.OrderId, cancellationToken);

            if (order is not null && order.CanTransitionTo(OrderStatus.Cancelled))
            {
                await RestoreStockAsync(order, cancellationToken);
                order.TransitionTo(OrderStatus.Cancelled, "gateway:payme");
            }

            txn.State = PaymentTransactionState.Cancelled;
            txn.CancelledAt = DateTimeOffset.UtcNow;
            txn.RawPayload = p.GetRawText();
            await db.SaveChangesAsync(cancellationToken);
        }

        return CancelResult(id, txn);
    }

    private async Task<object> CheckAsync(object? id, JsonElement p, CancellationToken cancellationToken)
    {
        var txn = await FindByPaymeIdAsync(GetTransactionId(p), cancellationToken);
        if (txn is null)
        {
            return Error(id, PaymeError.TransactionNotFound, "Transaction not found.");
        }

        return Result(id, new Dictionary<string, object?>
        {
            ["create_time"] = PaymeTime.ToUnixMs(txn.CreatedAt),
            ["perform_time"] = PaymeTime.ToUnixMs(txn.PerformedAt),
            ["cancel_time"] = PaymeTime.ToUnixMs(txn.CancelledAt),
            ["transaction"] = txn.Id.ToString(),
            ["state"] = PaymeState.Of(txn),
            ["reason"] = null
        });
    }

    private async Task<object> StatementAsync(object? id, JsonElement p, CancellationToken cancellationToken)
    {
        var from = p.TryGetProperty("from", out var fromEl) ? fromEl.GetInt64() : 0;
        var to = p.TryGetProperty("to", out var toEl) ? toEl.GetInt64() : long.MaxValue;
        var fromTime = DateTimeOffset.FromUnixTimeMilliseconds(from);
        var toTime = to == long.MaxValue ? DateTimeOffset.MaxValue : DateTimeOffset.FromUnixTimeMilliseconds(to);

        var transactions = await db.PaymentTransactions
            .Where(t => t.Provider == PaymentProvider.Payme && t.ProviderTxnId != null
                        && t.CreatedAt >= fromTime && t.CreatedAt <= toTime)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        var statement = transactions.Select(t => new Dictionary<string, object?>
        {
            ["id"] = t.ProviderTxnId,
            ["time"] = PaymeTime.ToUnixMs(t.CreatedAt),
            ["amount"] = PaymeAmount.ToTiyin(t.Amount),
            ["account"] = new Dictionary<string, object?> { [PaymeAccount.OrderIdField] = t.OrderId.ToString() },
            ["create_time"] = PaymeTime.ToUnixMs(t.CreatedAt),
            ["perform_time"] = PaymeTime.ToUnixMs(t.PerformedAt),
            ["cancel_time"] = PaymeTime.ToUnixMs(t.CancelledAt),
            ["transaction"] = t.Id.ToString(),
            ["state"] = PaymeState.Of(t)
        }).ToList();

        return Result(id, new Dictionary<string, object?> { ["transactions"] = statement });
    }

    private async Task<Order?> FindOrderAsync(JsonElement p, CancellationToken cancellationToken)
    {
        if (!p.TryGetProperty("account", out var account)
            || !account.TryGetProperty(PaymeAccount.OrderIdField, out var orderIdEl))
        {
            return null;
        }

        var orderNumber = orderIdEl.GetString();
        return orderNumber is null
            ? null
            : await db.Orders.FirstOrDefaultAsync(o => o.Number == orderNumber, cancellationToken);
    }

    private async Task RestoreStockAsync(Order order, CancellationToken cancellationToken)
    {
        var variantIds = order.Items.Select(i => i.ProductVariantId).ToList();
        var variants = await db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        foreach (var item in order.Items)
        {
            if (variants.TryGetValue(item.ProductVariantId, out var variant))
            {
                variant.StockQty += item.Qty;
            }
        }
    }

    private Task<PaymentTransaction?> FindByPaymeIdAsync(string? paymeId, CancellationToken cancellationToken) =>
        paymeId is null
            ? Task.FromResult<PaymentTransaction?>(null)
            : db.PaymentTransactions.FirstOrDefaultAsync(
                t => t.Provider == PaymentProvider.Payme && t.ProviderTxnId == paymeId, cancellationToken);

    private PaymentTransaction Track(PaymentTransaction txn)
    {
        db.PaymentTransactions.Add(txn);
        return txn;
    }

    private bool IsAuthorized(string? authorizationHeader)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorizationHeader["Basic ".Length..].Trim()));
            var separator = decoded.IndexOf(':');
            var key = separator >= 0 ? decoded[(separator + 1)..] : decoded;
            var expected = settings.Value.Payme.MerchantKey;
            return !string.IsNullOrEmpty(expected) && key == expected;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? GetTransactionId(JsonElement p) =>
        p.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;

    private static long GetAmount(JsonElement p) =>
        p.TryGetProperty("amount", out var amountEl) && amountEl.TryGetInt64(out var amount) ? amount : -1;

    private static object? ExtractId(JsonElement idEl) => idEl.ValueKind switch
    {
        JsonValueKind.Number => idEl.GetInt64(),
        JsonValueKind.String => idEl.GetString(),
        _ => null
    };

    private static object CreateResult(object? id, PaymentTransaction txn) => Result(id, new Dictionary<string, object?>
    {
        ["create_time"] = PaymeTime.ToUnixMs(txn.CreatedAt),
        ["transaction"] = txn.Id.ToString(),
        ["state"] = PaymeState.Created
    });

    private static object PerformResult(object? id, PaymentTransaction txn) => Result(id, new Dictionary<string, object?>
    {
        ["perform_time"] = PaymeTime.ToUnixMs(txn.PerformedAt),
        ["transaction"] = txn.Id.ToString(),
        ["state"] = PaymeState.Performed
    });

    private static object CancelResult(object? id, PaymentTransaction txn) => Result(id, new Dictionary<string, object?>
    {
        ["cancel_time"] = PaymeTime.ToUnixMs(txn.CancelledAt),
        ["transaction"] = txn.Id.ToString(),
        ["state"] = PaymeState.Of(txn)
    });

    private static object AccountError(object? id) => new Dictionary<string, object?>
    {
        ["error"] = new Dictionary<string, object?>
        {
            ["code"] = PaymeError.OrderNotFound,
            ["message"] = "Order not found.",
            ["data"] = PaymeAccount.OrderIdField
        },
        ["id"] = id
    };

    private static object Error(object? id, int code, string message) => new Dictionary<string, object?>
    {
        ["error"] = new Dictionary<string, object?> { ["code"] = code, ["message"] = message },
        ["id"] = id
    };

    private static object Result(object? id, object result) => new Dictionary<string, object?>
    {
        ["result"] = result,
        ["id"] = id
    };
}
