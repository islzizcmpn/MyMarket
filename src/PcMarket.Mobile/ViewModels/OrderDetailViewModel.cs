using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Orders;
using PcMarket.Mobile.Core;

namespace PcMarket.Mobile.ViewModels;

/// <summary>One order: totals, delivery address, item lines, and its status history. Offers pay-again while
/// payment is still outstanding, and cancel until the order has been paid for.</summary>
public partial class OrderDetailViewModel(
    OrdersApiClient orders,
    PaymentsApiClient payments,
    SessionGuard guard) : BaseViewModel, IQueryAttributable
{
    private Guid _orderId;

    [ObservableProperty]
    public partial OrderDto? Order { get; set; }

    public bool HasOrder => Order is not null;

    public string StatusText => Order is null ? string.Empty : Format.Status(Order.Status);

    public string TotalText => Format.Money(Order?.Total ?? 0m);

    public string SubtotalText => Format.Money(Order?.Subtotal ?? 0m);

    public string DeliveryFeeText => Format.Money(Order?.DeliveryFee ?? 0m);

    public string PaymentMethodText => Order is null ? string.Empty : Format.PaymentMethod(Order.PaymentMethod);

    public string CreatedAtText => Order is null ? string.Empty : Format.Date(Order.CreatedAt);

    public string AddressText => Order is null
        ? string.Empty
        : string.Join(", ", new[]
        {
            Order.ShippingAddress.Region,
            Order.ShippingAddress.City,
            Order.ShippingAddress.Street,
            Order.ShippingAddress.Details
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

    public bool CanPay => Order is { Status: OrderStatus.AwaitingPayment } && !IsBusy;

    /// <summary>Cancellable up to the point money has changed hands; after that it is a refund, which is a
    /// support/admin action rather than something the customer does from the app.</summary>
    public bool CanCancel => Order is { Status: OrderStatus.Created or OrderStatus.AwaitingPayment or OrderStatus.Processing } && !IsBusy;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && Guid.TryParse(id?.ToString(), out var parsed))
        {
            _orderId = parsed;
            Order = null;
        }
    }

    [RelayCommand]
    private Task AppearingAsync() => Order is null ? LoadAsync() : Task.CompletedTask;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async ct =>
    {
        Order = await guard.ExecuteAsync(c => orders.GetAsync(_orderId, c), ct);
        if (Order is null)
        {
            Error = "Order not found.";
        }
    });

    [RelayCommand]
    private Task PayAsync() => RunAsync(async ct =>
    {
        var initiation = await guard.ExecuteAsync(c => payments.InitiateAsync(_orderId, c), ct);

        if (initiation is { RequiresRedirect: true, PaymentUrl: { Length: > 0 } url })
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }

        Order = await guard.ExecuteAsync(c => orders.GetAsync(_orderId, c), ct);
    });

    [RelayCommand]
    private Task CancelAsync() => RunAsync(async ct =>
    {
        Order = await guard.ExecuteAsync(c => orders.CancelAsync(_orderId, c), ct);
    });

    protected override void OnBusyChanged()
    {
        OnPropertyChanged(nameof(CanPay));
        OnPropertyChanged(nameof(CanCancel));
    }

    partial void OnOrderChanged(OrderDto? value)
    {
        OnPropertyChanged(nameof(HasOrder));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(DeliveryFeeText));
        OnPropertyChanged(nameof(PaymentMethodText));
        OnPropertyChanged(nameof(CreatedAtText));
        OnPropertyChanged(nameof(AddressText));
        OnPropertyChanged(nameof(CanPay));
        OnPropertyChanged(nameof(CanCancel));
    }
}
