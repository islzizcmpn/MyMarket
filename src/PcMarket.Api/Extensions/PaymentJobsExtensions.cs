using Hangfire;
using PcMarket.Application.Orders;

namespace PcMarket.Api.Extensions;

/// <summary>Registers the recurring Hangfire jobs that keep the order/payment ledger consistent:
/// auto-cancelling unpaid orders past their timeout and reconciling settled-but-not-advanced orders.</summary>
public static class PaymentJobsExtensions
{
    public static WebApplication UsePaymentJobs(this WebApplication app)
    {
        var timeoutMinutes = app.Configuration.GetValue("Payments:UnpaidOrderTimeoutMinutes", 30);

        // Resolve the manager from this host's container rather than the static RecurringJob facade, which
        // binds to the process-global JobStorage.Current — unsafe when several hosts share a process (tests).
        using var scope = app.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        jobs.AddOrUpdate<OrderMaintenanceService>(
            "cancel-expired-orders",
            service => service.CancelExpiredOrdersAsync(timeoutMinutes, CancellationToken.None),
            Cron.Minutely());

        jobs.AddOrUpdate<OrderMaintenanceService>(
            "reconcile-pending-payments",
            service => service.ReconcilePendingPaymentsAsync(CancellationToken.None),
            "*/5 * * * *");

        return app;
    }
}
