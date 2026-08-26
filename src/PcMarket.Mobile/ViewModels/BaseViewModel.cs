using CommunityToolkit.Mvvm.ComponentModel;
using PcMarket.ApiClient;

namespace PcMarket.Mobile.ViewModels;

/// <summary>Shared busy/error handling. <see cref="RunAsync"/> is the single place API failures are turned
/// into something a customer can read, so no screen has to repeat the try/catch.</summary>
public abstract partial class BaseViewModel : ObservableObject
{
    /// <summary>How long a screen that caches its content stays good enough that returning to it does not
    /// refetch. Long enough that popping back from a product detail leaves the list — and, on the
    /// catalogue, the pages already scrolled through — exactly as it was; short enough that a tab
    /// returned to later shows current prices and stock.</summary>
    private static readonly TimeSpan Freshness = TimeSpan.FromMinutes(2);

    /// <summary>Re-entrancy guard for <see cref="RunAsync"/>. Deliberately not <see cref="IsBusy"/>:
    /// that one is bound to <c>RefreshView.IsRefreshing</c>, a TwoWay bindable property, so the
    /// pull-to-refresh gesture writes it <em>before</em> the command it triggers runs. Sharing the two
    /// made every pull cancel the very load it had just asked for and leave the spinner turning until
    /// the app was restarted — see docs/issues/mobile-refresh-never-completes/journal.md.</summary>
    private bool _running;

    private DateTimeOffset? _loadedAt;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public bool IsNotBusy => !IsBusy;

    /// <summary>Whether a screen that caches its content should load again: never loaded, explicitly
    /// invalidated, or last loaded longer ago than <see cref="Freshness"/>. Screens whose content must
    /// always be current — the cart, the order list — ignore this and load on every appearance.</summary>
    protected bool IsStale => _loadedAt is not { } loadedAt || DateTimeOffset.UtcNow - loadedAt > Freshness;

    protected async Task RunAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        if (_running)
        {
            return;
        }

        _running = true;
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
            _running = false;
            IsBusy = false;
        }
    }

    /// <summary>Records a successful load and starts the freshness window.</summary>
    protected void MarkLoaded() => _loadedAt = DateTimeOffset.UtcNow;

    /// <summary>Forces the next appearance to reload, whatever the freshness window says.</summary>
    protected void Invalidate() => _loadedAt = null;

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
