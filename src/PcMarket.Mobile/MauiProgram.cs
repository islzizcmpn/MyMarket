using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using PcMarket.ApiClient;
using PcMarket.Mobile.Core;
using PcMarket.Mobile.Services;
using PcMarket.Mobile.ViewModels;
using PcMarket.Mobile.Views;

namespace PcMarket.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddPcMarketApiClient(AppConfig.ApiRootUrl);

		// One session per install, so everything session-shaped is a singleton.
		builder.Services.AddSingleton<ISessionStorage, MauiSessionStorage>();
		builder.Services.AddSingleton<MobileSession>();
		builder.Services.AddSingleton<IApiTokenProvider, MobileApiTokenProvider>();
		builder.Services.AddSingleton<SessionGuard>();
		builder.Services.AddSingleton<StoreCart>();
		builder.Services.AddSingleton<PushRegistrar>();
		builder.Services.AddSingleton<AuthFlow>();
		builder.Services.AddSingleton<IPushTokenSource, PushTokenSource>();

		builder.Services.AddSingleton<AppShell>();

		AddPage<HomePage, HomeViewModel>(builder.Services);
		AddPage<CatalogPage, CatalogViewModel>(builder.Services);
		AddPage<CartPage, CartViewModel>(builder.Services);
		AddPage<AccountPage, AccountViewModel>(builder.Services);
		AddPage<ProductPage, ProductViewModel>(builder.Services);
		AddPage<CheckoutPage, CheckoutViewModel>(builder.Services);
		AddPage<OrdersPage, OrdersViewModel>(builder.Services);
		AddPage<OrderDetailPage, OrderDetailViewModel>(builder.Services);
		AddPage<AddressesPage, AddressesViewModel>(builder.Services);
		AddPage<LoginPage, LoginViewModel>(builder.Services);
		AddPage<RegisterPage, RegisterViewModel>(builder.Services);
		AddPage<OtpPage, OtpViewModel>(builder.Services);

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	/// <summary>Pages and view models are transient: the tab pages are resolved once when the shell is built
	/// and live as long as it does, while a pushed page gets a fresh view model each time it is opened.</summary>
	private static void AddPage<TPage, TViewModel>(IServiceCollection services)
		where TPage : ContentPage
		where TViewModel : ObservableObject
	{
		services.AddTransient<TPage>();
		services.AddTransient<TViewModel>();
	}
}
