using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using PcMarket.ApiClient;
using PcMarket.Admin.Services;

namespace PcMarket.Admin.Components;

/// <summary>Base for authenticated admin pages: hydrates the session, redirects to the login page when
/// the caller isn't signed in, and hands every page the shared string table as <c>L</c>.</summary>
public abstract class AdminPageBase : ComponentBase
{
    [Inject] protected AdminSession Session { get; set; } = default!;
    [Inject] protected SessionStore Store { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected AdminApiClient Api { get; set; } = default!;
    [Inject] protected IStringLocalizer<SharedResource> L { get; set; } = default!;

    protected async Task<bool> EnsureAuthedAsync()
    {
        if (!Session.Loaded)
        {
            await Store.LoadIntoAsync(Session);
        }

        if (!Session.IsAuthenticated)
        {
            Nav.NavigateTo("/login");
            return false;
        }

        return true;
    }
}
