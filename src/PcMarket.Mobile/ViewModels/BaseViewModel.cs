using CommunityToolkit.Mvvm.ComponentModel;
using PcMarket.ApiClient;

namespace PcMarket.Mobile.ViewModels;

/// <summary>Shared busy/error handling. <see cref="RunAsync"/> is the single place API failures are turned
/// into something a customer can read, so no screen has to repeat the try/catch.</summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public bool IsNotBusy => !IsBusy;

    protected async Task RunAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Error = null;

        try
        {
            await work(cancellationToken);
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
        }
        catch (HttpRequestException)
        {
            Error = "Can't reach the store. Check your connection and try again.";
        }
        catch (TaskCanceledException)
        {
            Error = "The request timed out. Try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Hook for screens whose derived state (e.g. "empty list") also depends on busy-ness.</summary>
    protected virtual void OnBusyChanged()
    {
    }

    partial void OnErrorChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        OnBusyChanged();
    }
}
