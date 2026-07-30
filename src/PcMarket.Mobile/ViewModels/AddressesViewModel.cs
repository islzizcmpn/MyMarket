using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Users;
using PcMarket.Mobile.Core;

namespace PcMarket.Mobile.ViewModels;

/// <summary>Saved delivery addresses: list, add, edit, delete, and choose the default.</summary>
public partial class AddressesViewModel(UsersApiClient users, SessionGuard guard) : BaseViewModel
{
    public ObservableCollection<AddressDto> Addresses { get; } = [];

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial Guid? EditingId { get; set; }

    [ObservableProperty]
    public partial string? Region { get; set; }

    [ObservableProperty]
    public partial string? City { get; set; }

    [ObservableProperty]
    public partial string? Street { get; set; }

    [ObservableProperty]
    public partial string? Details { get; set; }

    [ObservableProperty]
    public partial bool IsDefault { get; set; }

    public string EditorTitle => EditingId is null ? "New address" : "Edit address";

    public bool IsEmpty => Addresses.Count == 0 && !IsBusy && !IsEditing;

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

        OnPropertyChanged(nameof(IsEmpty));
    });

    [RelayCommand]
    private void StartAdd()
    {
        EditingId = null;
        Region = City = Street = Details = null;
        IsDefault = Addresses.Count == 0;
        IsEditing = true;
    }

    [RelayCommand]
    private void StartEdit(AddressDto? address)
    {
        if (address is null)
        {
            return;
        }

        EditingId = address.Id;
        Region = address.Region;
        City = address.City;
        Street = address.Street;
        Details = address.Details;
        IsDefault = address.IsDefault;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async ct =>
    {
        if (string.IsNullOrWhiteSpace(Region) || string.IsNullOrWhiteSpace(City) || string.IsNullOrWhiteSpace(Street))
        {
            Error = "Region, city, and street are required.";
            return;
        }

        var details = string.IsNullOrWhiteSpace(Details) ? null : Details.Trim();

        if (EditingId is { } id)
        {
            await guard.ExecuteAsync(
                c => users.UpdateAddressAsync(id, new UpdateAddressRequest(Region.Trim(), City.Trim(), Street.Trim(), details, IsDefault), c),
                ct);
        }
        else
        {
            await guard.ExecuteAsync(
                c => users.CreateAddressAsync(new CreateAddressRequest(Region.Trim(), City.Trim(), Street.Trim(), details, IsDefault), c),
                ct);
        }

        IsEditing = false;
        await ReloadAsync(ct);
    });

    [RelayCommand]
    private Task DeleteAsync(AddressDto? address) => address is null
        ? Task.CompletedTask
        : RunAsync(async ct =>
        {
            await guard.ExecuteAsync(c => users.DeleteAddressAsync(address.Id, c), ct);
            await ReloadAsync(ct);
        });

    private async Task ReloadAsync(CancellationToken ct)
    {
        var saved = await guard.ExecuteAsync(users.ListAddressesAsync, ct);

        Addresses.Clear();
        foreach (var address in saved)
        {
            Addresses.Add(address);
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    protected override void OnBusyChanged() => OnPropertyChanged(nameof(IsEmpty));

    partial void OnEditingIdChanged(Guid? value) => OnPropertyChanged(nameof(EditorTitle));

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
}
