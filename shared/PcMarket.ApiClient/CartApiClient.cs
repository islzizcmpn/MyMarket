using PcMarket.Contracts.Cart;

namespace PcMarket.ApiClient;

/// <summary>Typed access to the cart endpoints. The guest-cart token travels via the
/// <c>X-Cart-Token</c> header injected by <see cref="ApiClientBase"/>.</summary>
public sealed class CartApiClient(HttpClient http, IApiTokenProvider tokens) : ApiClientBase(http, tokens)
{
    public Task<CartDto> GetAsync(CancellationToken cancellationToken = default) =>
        GetAsync<CartDto>("cart", cancellationToken);

    public Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<AddCartItemRequest, CartDto>("cart/items", request, cancellationToken);

    public Task<CartDto> UpdateItemAsync(Guid itemId, UpdateCartItemRequest request, CancellationToken cancellationToken = default) =>
        PutAsync<UpdateCartItemRequest, CartDto>($"cart/items/{itemId}", request, cancellationToken);

    public Task<CartDto> RemoveItemAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        DeleteWithResultAsync<CartDto>($"cart/items/{itemId}", cancellationToken);

    public Task<CartDto> MergeAsync(string token, CancellationToken cancellationToken = default) =>
        PostAsync<CartDto>($"cart/merge?token={Uri.EscapeDataString(token)}", cancellationToken);
}
