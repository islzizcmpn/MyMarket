using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PcMarket.Application.Abstractions.Payments;
using PcMarket.Payments.Click;
using PcMarket.Payments.Configuration;
using PcMarket.Payments.Payme;
using PcMarket.Payments.Providers;

namespace PcMarket.Payments;

/// <summary>Registers the payment rails, provider resolver, and gateway callback handlers.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PaymentsSettings>(configuration.GetSection("Payments"));

        services.AddScoped<IPaymentProvider, CashPaymentProvider>();
        services.AddScoped<IPaymentProvider, ClickPaymentProvider>();
        services.AddScoped<IPaymentProvider, PaymePaymentProvider>();
        services.AddScoped<IPaymentProvider, UzcardPaymentProvider>();
        services.AddScoped<IPaymentProvider, HumoPaymentProvider>();

        services.AddScoped<IPaymentProviderResolver, PaymentProviderResolver>();

        services.AddScoped<ClickCallbackService>();
        services.AddScoped<PaymeRpcService>();

        return services;
    }
}
