namespace PcMarket.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        // Loads Application.Resources (colours, styles, converters) — every page's StaticResource
        // lookups resolve against them, so this must run before the first page is built.
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState) =>
        // The stored session is hydrated lazily by the API token provider, so start-up never blocks on a
        // keystore read; see MobileApiTokenProvider.
        new(_services.GetRequiredService<AppShell>());
}
