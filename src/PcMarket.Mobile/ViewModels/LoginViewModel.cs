using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Auth;
using PcMarket.Mobile.Services;

namespace PcMarket.Mobile.ViewModels;

/// <summary>Phone + password sign-in. <c>returnTo</c> carries where the user was headed (checkout, usually)
/// so they land there instead of back on the login screen.</summary>
public partial class LoginViewModel(AuthApiClient auth, AuthFlow flow) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    public partial string? Phone { get; set; }

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
        var response = await auth.LoginAsync(new LoginRequest(Phone?.Trim() ?? string.Empty, Password ?? string.Empty), ct);
        await flow.CompleteSignInAsync(response, ct);
        await ReturnTargets.NavigateAsync(_returnTo);
    });

    [RelayCommand]
    private Task GoToRegisterAsync() =>
        Shell.Current.GoToAsync($"register{(_returnTo is null ? string.Empty : $"?returnTo={_returnTo}")}");
}
