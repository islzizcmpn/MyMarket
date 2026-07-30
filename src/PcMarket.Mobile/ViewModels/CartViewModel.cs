using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.Contracts.Cart;
using PcMarket.Mobile.Core;
using PcMarket.Mobile.Services;

namespace PcMarket.Mobile.ViewModels;

/// <summary>The cart tab: quantities, removal, and the hand-off to checkout.</summary>
public partial class CartViewModel(StoreCart cart, MobileSession session) : BaseViewModel
{
    public ObservableCollection<CartItemDto> Items { get; } = [];

    public string SubtotalText => Format.Money(cart.Current?.Subtotal ?? 0m);

    public bool IsEmpty => Items.Count == 0 && !IsBusy;

    public bool CanCheckout => Items.Count > 0 && !IsBusy;

    [RelayCommand]
    private Task AppearingAsync() => LoadAsync();

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async ct =>
    {
        var dto = await cart.RefreshAsync(ct);
        Apply(dto);
    });

    [RelayCommand]
    private Task IncreaseAsync(CartItemDto? item) => item is null
        ? Task.CompletedTask
        : RunAsync(async ct => Apply(await cart.UpdateAsync(item.Id, item.Qty + 1, ct)));

    /// <summary>Quantity 0 is how the API removes an item, so the last decrement doubles as a remove.</summary>
    [RelayCommand]
    private Task DecreaseAsync(CartItemDto? item) => item is null
        ? Task.CompletedTask
        : RunAsync(async ct => Apply(await cart.UpdateAsync(item.Id, item.Qty - 1, ct)));

    [RelayCommand]
    private Task RemoveAsync(CartItemDto? item) => item is null
        ? Task.CompletedTask
        : RunAsync(async ct => Apply(await cart.RemoveAsync(item.Id, ct)));

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (!session.IsAuthenticated)
        {
            // Checkout needs an account; come back here after signing in.
            await Shell.Current.GoToAsync("login?returnTo=checkout");
            return;
        }

        await Shell.Current.GoToAsync("checkout");
    }

    private void Apply(CartDto dto)
    {
        Items.Clear();
        foreach (var item in dto.Items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanCheckout));
    }

    protected override void OnBusyChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanCheckout));
    }
}
