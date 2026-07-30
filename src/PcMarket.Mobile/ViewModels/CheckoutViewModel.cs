using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Orders;
using PcMarket.Contracts.Users;
using PcMarket.Mobile.Core;
using PcMarket.Mobile.Services;

namespace PcMarket.Mobile.ViewModels;

/// <summary>Turns the cart into an order: pick a saved address or type a new one, choose delivery and
/// payment, then create the order and start its payment. Online rails hand off to the gateway in the system
/// browser; cash lands straight on the order.</summary>
public partial class CheckoutViewModel(
    UsersApiClient users,
    OrdersApiClient orders,
    PaymentsApiClient payments,
    StoreCart cart,
    SessionGuard guard) : BaseViewModel
{
    public ObservableCollection<AddressDto> Addresses { get; } = [];

    public IReadOnlyList<PaymentMethod> PaymentMethods { get; } = Enum.GetValues<PaymentMethod>();

    public IReadOnlyList<DeliveryType> DeliveryTypes { get; } = Enum.GetValues<DeliveryType>();

    [ObservableProperty]
    public partial AddressDto? SelectedAddress { get; set; }

    [ObservableProperty]
    public partial bool UseNewAddress { get; set; }

    [ObservableProperty]
    public partial string? Region { get; set; }

    [ObservableProperty]
    public partial string? City { get; set; }

    [ObservableProperty]
    public partial string? Street { get; set; }

    [ObservableProperty]
    public partial string? Details { get; set; }

    /// <summary>Cash and Courier are the zero values of their enums, which is the default a partial
    /// property gets — and the right default for this market anyway.</summary>
    [ObservableProperty]
    public partial PaymentMethod PaymentMethod { get; set; }

    [ObservableProperty]
    public partial DeliveryType DeliveryType { get; set; }

    public string TotalText => Format.Money(cart.Current?.Subtotal ?? 0m);

    public bool HasSavedAddresses => Addresses.Count > 0;

    /// <summary>Pickup needs no address; courier does, either saved or typed in.</summary>
    public bool AddressRequired => DeliveryType == DeliveryType.Courier;

    [RelayCommand]
    private Task AppearingAsync() => LoadAsync();

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async ct =>
    {
        var saved = await guard.ExecuteAsync(users.ListAddressesAsync, ct);

        Addresses.Clear();
        foreach (var address in saved)
        {
            Addresses.Add(address);
        }

        SelectedAddress = Addresses.FirstOrDefault(a => a.IsDefault) ?? Addresses.FirstOrDefault();
        UseNewAddress = Addresses.Count == 0;

        OnPropertyChanged(nameof(HasSavedAddresses));
        OnPropertyChanged(nameof(TotalText));
    });

    [RelayCommand]
    private Task PlaceOrderAsync() => RunAsync(async ct =>
    {
        var request = BuildRequest();
        if (request is null)
        {
            return;
        }

        var order = await guard.ExecuteAsync(c => orders.CreateAsync(request, c), ct);

        // The cart is emptied server-side by the order; refresh so the badge clears.
        await cart.RefreshAsync(ct);

        var initiation = await guard.ExecuteAsync(c => payments.InitiateAsync(order.Id, c), ct);
        if (initiation is { RequiresRedirect: true, PaymentUrl: { Length: > 0 } url })
        {
            await Browser.Default.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
        }

        await Shell.Current.GoToAsync($"//account/orders/order?id={order.Id}");
    });

    private CreateOrderRequest? BuildRequest()
    {
        if (!AddressRequired)
        {
            return new CreateOrderRequest(PaymentMethod, DeliveryType, null, null);
        }

        if (!UseNewAddress)
        {
            if (SelectedAddress is null)
            {
                Error = "Choose a delivery address.";
                return null;
            }

            return new CreateOrderRequest(PaymentMethod, DeliveryType, SelectedAddress.Id, null);
        }

        if (string.IsNullOrWhiteSpace(Region) || string.IsNullOrWhiteSpace(City) || string.IsNullOrWhiteSpace(Street))
        {
            Error = "Region, city, and street are required.";
            return null;
        }

        var address = new ShippingAddressDto(
            Region.Trim(),
            City.Trim(),
            Street.Trim(),
            string.IsNullOrWhiteSpace(Details) ? null : Details.Trim());

        return new CreateOrderRequest(PaymentMethod, DeliveryType, null, address);
    }

    partial void OnDeliveryTypeChanged(DeliveryType value) => OnPropertyChanged(nameof(AddressRequired));
}
