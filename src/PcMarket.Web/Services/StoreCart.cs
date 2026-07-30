using PcMarket.ApiClient;
using PcMarket.Contracts.Cart;

namespace PcMarket.Web.Services;

/// <summary>Web-side cart facade over <see cref="CartApiClient"/>. Every call captures the guest-cart token
/// the API hands back (persisting it so the cart survives reloads) and refreshes the header badge count.</summary>
public sealed class StoreCart(CartApiClient api, WebSession session, SessionStore store, CartState state)
{
    public async Task<CartDto> GetAsync(CancellationToken ct = default) => await SyncAsync(await api.GetAsync(ct));

    public async Task<CartDto> AddAsync(Guid variantId, int qty, CancellationToken ct = default) =>
        await SyncAsync(await api.AddItemAsync(new AddCartItemRequest(variantId, qty), ct));

    public async Task<CartDto> UpdateAsync(Guid itemId, int qty, CancellationToken ct = default) =>
        await SyncAsync(await api.UpdateItemAsync(itemId, new UpdateCartItemRequest(qty), ct));

    public async Task<CartDto> RemoveAsync(Guid itemId, CancellationToken ct = default) =>
        await SyncAsync(await api.RemoveItemAsync(itemId, ct));

    /// <summary>Merges the guest cart into the signed-in user's cart after login, then drops the guest token.</summary>
    public async Task MergeGuestAsync(CancellationToken ct = default)
    {
        var guestToken = session.CartToken;
        if (string.IsNullOrEmpty(guestToken))
        {
            return;
        }

        var merged = await api.MergeAsync(guestToken, ct);
        session.SetCartToken(null);
        await store.SaveAsync(session);
        state.Set(merged.TotalQty);
    }

    private async Task<CartDto> SyncAsync(CartDto cart)
    {
        if (!string.IsNullOrEmpty(cart.Token) && session.CartToken != cart.Token)
        {
            session.SetCartToken(cart.Token);
            await store.SaveAsync(session);
        }

        state.Set(cart.TotalQty);
        return cart;
    }
}
