using PcMarket.Mobile.Views;

namespace PcMarket.Mobile;

public partial class AppShell : Shell
{
    public AppShell(HomePage home, CatalogPage catalog, CartPage cart, AccountPage account)
    {
        InitializeComponent();

        HomeTab.Content = home;
        CatalogTab.Content = catalog;
        CartTab.Content = cart;
        AccountTab.Content = account;

        RegisterRoutes();
    }

    /// <summary>Pages pushed on top of a tab. Registered routes are resolved through the DI container, so
    /// these pages get their view models injected the same way the tabs do.</summary>
    private static void RegisterRoutes()
    {
        Routing.RegisterRoute("product", typeof(ProductPage));
        Routing.RegisterRoute("checkout", typeof(CheckoutPage));
        Routing.RegisterRoute("orders", typeof(OrdersPage));
        Routing.RegisterRoute("order", typeof(OrderDetailPage));
        Routing.RegisterRoute("addresses", typeof(AddressesPage));
        Routing.RegisterRoute("login", typeof(LoginPage));
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("otp", typeof(OtpPage));
    }
}
