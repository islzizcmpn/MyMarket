using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Users;
using PcMarket.Mobile.Core;
using PcMarket.Mobile.Services;

namespace PcMarket.Mobile.ViewModels;

/// <summary>The account tab. Signed out it offers sign-in/registration; signed in it shows the profile and
/// the way through to orders and addresses.</summary>
public partial class AccountViewModel(
    UsersApiClient users,
    MobileSession session,
    SessionGuard guard,
    AuthFlow flow,
    ThemeService theme) : BaseViewModel
{
    [ObservableProperty]
    public partial UserProfileDto? Profile { get; set; }

    public bool IsAuthenticated => session.IsAuthenticated;

    public bool IsAnonymous => !session.IsAuthenticated;

    /// <summary>Drives the appearance toggle. Read from the service rather than mirrored into a
    /// field, so a theme applied from anywhere else is still what the control shows.</summary>
    public bool IsDarkTheme => theme.Current == AppTheme.Dark;

    public bool IsLightTheme => !IsDarkTheme;

    public string DisplayName => Profile?.FullName is { Length: > 0 } name ? name : Profile?.Phone ?? string.Empty;

    [RelayCommand]
    private Task AppearingAsync() => LoadAsync();

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async ct =>
    {
        // The account tab reads the session directly, so make sure it has been restored from storage first.
        await session.LoadAsync(ct);
        NotifyAuthState();

        if (!session.IsAuthenticated)
        {
            Profile = null;
            return;
        }

        Profile = await guard.ExecuteAsync(users.GetProfileAsync, ct);
        OnPropertyChanged(nameof(DisplayName));
    });

    [RelayCommand]
    private static Task SignInAsync() => Shell.Current.GoToAsync("login");

    [RelayCommand]
    private static Task RegisterAsync() => Shell.Current.GoToAsync("register");

    [RelayCommand]
    private static Task OpenOrdersAsync() => Shell.Current.GoToAsync("orders");

    [RelayCommand]
    private static Task OpenAddressesAsync() => Shell.Current.GoToAsync("addresses");

    [RelayCommand]
    private void UseDarkTheme() => SetTheme(AppTheme.Dark);

    [RelayCommand]
    private void UseLightTheme() => SetTheme(AppTheme.Light);

    private void SetTheme(AppTheme value)
    {
        theme.Apply(value);
        OnPropertyChanged(nameof(IsDarkTheme));
        OnPropertyChanged(nameof(IsLightTheme));
    }

    [RelayCommand]
    private Task SignOutAsync() => RunAsync(async ct =>
    {
        await flow.SignOutAsync(ct);
        Profile = null;
        NotifyAuthState();
    });

    private void NotifyAuthState()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsAnonymous));
        OnPropertyChanged(nameof(DisplayName));
    }
}
