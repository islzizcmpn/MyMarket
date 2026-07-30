using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Orders;
using PcMarket.Mobile.Core;

namespace PcMarket.Mobile.ViewModels;

/// <summary>The signed-in customer's order history.</summary>
public partial class OrdersViewModel(OrdersApiClient orders, SessionGuard guard) : BaseViewModel
{
    public ObservableCollection<OrderListItemDto> Orders { get; } = [];

    public bool IsEmpty => Orders.Count == 0 && !IsBusy;

    [RelayCommand]
    private Task AppearingAsync() => LoadAsync();

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async ct =>
    {
        var list = await guard.ExecuteAsync(orders.ListAsync, ct);

        Orders.Clear();
        foreach (var order in list)
        {
            Orders.Add(order);
        }

        OnPropertyChanged(nameof(IsEmpty));
    });

    [RelayCommand]
    private static Task OpenAsync(OrderListItemDto? order) =>
        order is null ? Task.CompletedTask : Shell.Current.GoToAsync($"order?id={order.Id}");

    protected override void OnBusyChanged() => OnPropertyChanged(nameof(IsEmpty));
}
