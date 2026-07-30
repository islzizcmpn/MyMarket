using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Auth;

namespace PcMarket.Mobile.ViewModels;

/// <summary>Phone-first registration. The account is not usable until the OTP sent to that phone is
/// verified, so this screen hands straight off to the OTP step.</summary>
public partial class RegisterViewModel(AuthApiClient auth) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    public partial string? Phone { get; set; }

    [ObservableProperty]
    public partial string? FullName { get; set; }

    [ObservableProperty]
    public partial string? Password { get; set; }

    private string? _returnTo;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("returnTo", out var returnTo))
        {
            _returnTo = returnTo?.ToString();
        }
    }

    [RelayCommand]
    private Task SubmitAsync() => RunAsync(async ct =>
    {
        var phone = Phone?.Trim() ?? string.Empty;
        await auth.RegisterAsync(
            new RegisterRequest(phone, Password ?? string.Empty, string.IsNullOrWhiteSpace(FullName) ? null : FullName.Trim()),
            ct);

        var route = $"otp?phone={Uri.EscapeDataString(phone)}";
        if (_returnTo is not null)
        {
            route += $"&returnTo={_returnTo}";
        }

        await Shell.Current.GoToAsync(route);
    });
}
