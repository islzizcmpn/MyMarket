using Microsoft.Extensions.DependencyInjection;

namespace PcMarket.ApiClient;

/// <summary>Registers the typed API clients. The caller must also register an <see cref="IApiTokenProvider"/>
/// implementation for its session model.</summary>
public static class ServiceCollectionExtensions
{
    /// <param name="apiRootUrl">The API host root, e.g. <c>http://localhost:5055</c>; the <c>/api/v1/</c>
    /// prefix is appended automatically.</param>
    public static IServiceCollection AddPcMarketApiClient(this IServiceCollection services, string apiRootUrl)
    {
        var root = apiRootUrl.EndsWith('/') ? apiRootUrl : apiRootUrl + "/";
        var baseAddress = new Uri(new Uri(root), "api/v1/");

        Register<CatalogApiClient>(services, baseAddress);
        Register<CartApiClient>(services, baseAddress);
        Register<AuthApiClient>(services, baseAddress);
        Register<OrdersApiClient>(services, baseAddress);
        Register<PaymentsApiClient>(services, baseAddress);
        Register<UsersApiClient>(services, baseAddress);
        Register<AdminApiClient>(services, baseAddress);
        Register<ContentApiClient>(services, baseAddress);
        Register<ConfiguratorApiClient>(services, baseAddress);

        return services;
    }

    private static void Register<TClient>(IServiceCollection services, Uri baseAddress) where TClient : class =>
        services.AddHttpClient<TClient>(client => client.BaseAddress = baseAddress);
}
