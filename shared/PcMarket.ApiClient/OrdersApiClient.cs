using PcMarket.Contracts.Orders;

namespace PcMarket.ApiClient;

/// <summary>Typed access to the order endpoints (all require authentication).</summary>
public sealed class OrdersApiClient(HttpClient http, IApiTokenProvider tokens) : ApiClientBase(http, tokens)
{
    public Task<OrderDto> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CreateOrderRequest, OrderDto>("orders", request, cancellationToken);

    public Task<IReadOnlyList<OrderListItemDto>> ListAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<OrderListItemDto>>("orders", cancellationToken);

    public Task<OrderDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<OrderDto>($"orders/{id}", cancellationToken);

    public Task<OrderDto> CancelAsync(Guid id, CancellationToken cancellationToken = default) =>
        PostAsync<OrderDto>($"orders/{id}/cancel", cancellationToken);
}
