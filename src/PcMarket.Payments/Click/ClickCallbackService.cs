using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Domain.Enums;
using PcMarket.Domain.Ordering;
using PcMarket.Domain.Payments;
using PcMarket.Payments.Configuration;

namespace PcMarket.Payments.Click;

/// <summary>Handles Click's server-to-server Prepare/Complete callbacks: verifies the signature and amount,
/// updates the <see cref="PaymentTransaction"/> ledger idempotently, and drives the order to Paid on
/// completion. Replaying either callback produces the same ledger state and response.</summary>
public sealed class ClickCallbackService(IApplicationDbContext db, IOptions<PaymentsSettings> settings)
{
    private const decimal AmountTolerance = 0.01m;

    public async Task<IReadOnlyDictionary<string, object?>> HandleAsync(
        ClickCallbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ClickSignature.IsValid(request, settings.Value.Click.SecretKey))
        {
            return Respond(request, ClickError.SignCheckFailed, "Sign check failed.");
        }

        return request.Action switch
        {
            ClickAction.Prepare => await PrepareAsync(request, cancellationToken),
            ClickAction.Complete => await CompleteAsync(request, cancellationToken),
            _ => Respond(request, ClickError.ActionNotFound, "Action not found.")
        };
    }

    private async Task<IReadOnlyDictionary<string, object?>> PrepareAsync(ClickCallbackRequest request, CancellationToken cancellationToken)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Number == request.MerchantTransId, cancellationToken);
        if (order is null)
        {
            return Respond(request, ClickError.OrderNotFound, "Order not found.");
        }

        if (order.Status is OrderStatus.Paid or OrderStatus.Processing or OrderStatus.Shipped or OrderStatus.Delivered)
        {
            return Respond(request, ClickError.AlreadyPaid, "Order already paid.");
        }

        if (!AmountMatches(request.Amount, order.Total))
        {
            return Respond(request, ClickError.InvalidAmount, "Incorrect amount.");
        }

        var txn = await FindByClickTxnAsync(request.ClickTransId, cancellationToken);
        if (txn is null)
        {
            txn = await db.PaymentTransactions.FirstOrDefaultAsync(
                      t => t.OrderId == order.Id && t.State == PaymentTransactionState.Created, cancellationToken)
                  ?? Track(new PaymentTransaction { OrderId = order.Id, Provider = PaymentProvider.Click });

            txn.ProviderTxnId = request.ClickTransId;
            txn.State = PaymentTransactionState.Pending;
            txn.Amount = order.Total;
            txn.RawPayload = Serialize(request);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Respond(request, ClickError.Success, "Success", prepareId: request.ClickTransId);
    }

    private async Task<IReadOnlyDictionary<string, object?>> CompleteAsync(ClickCallbackRequest request, CancellationToken cancellationToken)
    {
        var txn = await FindByClickTxnAsync(request.ClickTransId, cancellationToken);
        if (txn is null || request.MerchantPrepareId != request.ClickTransId)
        {
            return Respond(request, ClickError.TransactionNotFound, "Transaction not found.");
        }

        if (txn.State == PaymentTransactionState.Cancelled)
        {
            return Respond(request, ClickError.TransactionCancelled, "Transaction cancelled.");
        }

        var order = await db.Orders
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == txn.OrderId, cancellationToken);
        if (order is null)
        {
            return Respond(request, ClickError.OrderNotFound, "Order not found.");
        }

        // Click signals a failed payment by sending a negative error code on Complete.
        if (request.Error < 0)
        {
            txn.State = PaymentTransactionState.Cancelled;
            txn.CancelledAt = DateTimeOffset.UtcNow;
            txn.RawPayload = Serialize(request);
            await db.SaveChangesAsync(cancellationToken);
            return Respond(request, request.Error, request.ErrorNote ?? "Payment failed.");
        }

        // Idempotent: a replayed Complete for an already-performed transaction re-acknowledges success.
        if (txn.State != PaymentTransactionState.Performed)
        {
            txn.State = PaymentTransactionState.Performed;
            txn.PerformedAt = DateTimeOffset.UtcNow;
            txn.RawPayload = Serialize(request);

            if (order.Status == OrderStatus.AwaitingPayment)
            {
                order.TransitionTo(OrderStatus.Paid, "gateway:click");
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return Respond(request, ClickError.Success, "Success", confirmId: request.ClickTransId);
    }

    private Task<PaymentTransaction?> FindByClickTxnAsync(string clickTransId, CancellationToken cancellationToken) =>
        db.PaymentTransactions.FirstOrDefaultAsync(t => t.ProviderTxnId == clickTransId, cancellationToken);

    private PaymentTransaction Track(PaymentTransaction txn)
    {
        db.PaymentTransactions.Add(txn);
        return txn;
    }

    private static bool AmountMatches(string amount, decimal expected) =>
        decimal.TryParse(amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
        && Math.Abs(value - expected) <= AmountTolerance;

    private static string Serialize(ClickCallbackRequest request) => JsonSerializer.Serialize(request);

    private static IReadOnlyDictionary<string, object?> Respond(
        ClickCallbackRequest request,
        int error,
        string note,
        string? prepareId = null,
        string? confirmId = null)
    {
        var response = new Dictionary<string, object?>
        {
            ["click_trans_id"] = request.ClickTransId,
            ["merchant_trans_id"] = request.MerchantTransId,
            ["error"] = error,
            ["error_note"] = note
        };

        if (prepareId is not null)
        {
            response["merchant_prepare_id"] = prepareId;
        }

        if (confirmId is not null)
        {
            response["merchant_confirm_id"] = confirmId;
        }

        return response;
    }
}
