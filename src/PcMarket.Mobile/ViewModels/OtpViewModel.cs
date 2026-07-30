using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PcMarket.ApiClient;
using PcMarket.Contracts.Auth;
using PcMarket.Mobile.Services;

namespace PcMarket.Mobile.ViewModels;

/// <summary>Verifies the SMS code and completes registration. In development the code is written to the API
/// log rather than sent, since no SMS provider is wired up yet.</summary>
public partial class OtpViewModel(AuthApiClient auth, AuthFlow flow) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    public partial string? Phone { get; set; }

    [ObservableProperty]
    public partial string? Code { get; set; }

    private string? _returnTo;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("phone", out var phone))
        {
            Phone = Uri.UnescapeDataString(phone?.ToString() ?? string.Empty);
        }

        if (query.TryGetValue("returnTo", out var returnTo))
        {
            _returnTo = returnTo?.ToString();
        }
    }

    [RelayCommand]
    private Task SubmitAsync() => RunAsync(async ct =>
    {
        var response = await auth.VerifyOtpAsync(
            new VerifyOtpRequest(Phone ?? string.Empty, Code?.Trim() ?? string.Empty), ct);
        await flow.CompleteSignInAsync(response, ct);
        await ReturnTargets.NavigateAsync(_returnTo, "//account");
    });
}
