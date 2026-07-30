using PcMarket.ApiClient;
using PcMarket.Contracts.Cart;
using PcMarket.Mobile.Core;

namespace PcMarket.Mobile.Services;

/// <summary>The app's single view of the cart. Every mutation returns the server's authoritative cart, whose
/// guest token is persisted so an anonymous cart survives a restart; the tab badge reads <see cref="TotalQty"/>.</summary>
public sealed class StoreCart(CartApiClient cart, MobileSession session, SessionGuard guard)
{
    public CartDto? Current { get; private set; }

    public int TotalQty => Current?.TotalQty ?? 0;

    public event Action? Changed;

    public Task<CartDto> RefreshAsync(CancellationToken cancellationToken = default) =>
        ApplyAsync(ct => cart.GetAsync(ct), cancellationToken);

    public Task<CartDto> AddAsync(Guid variantId, int qty, CancellationToken cancellationToken = default) =>
        ApplyAsync(ct => cart.AddItemAsync(new AddCartItemRequest(variantId, qty), ct), cancellationToken);

    public Task<CartDto> UpdateAsync(Guid itemId, int qty, CancellationToken cancellationToken = default) =>
        ApplyAsync(ct => cart.UpdateItemAsync(itemId, new UpdateCartItemRequest(qty), ct), cancellationToken);

    public Task<CartDto> RemoveAsync(Guid itemId, CancellationToken cancellationToken = default) =>
        ApplyAsync(ct => cart.RemoveItemAsync(itemId, ct), cancellationToken);

    /// <summary>Folds the guest cart into the freshly signed-in user's cart, then drops the guest token so
    /// later requests are attributed to the account alone.</summary>
    public async Task MergeGuestCartAsync(CancellationToken cancellationToken = default)
    {
        var guestToken = session.CartToken;
        if (string.IsNullOrEmpty(guestToken))
        {
            await RefreshAsync(cancellationToken);
            return;
        }

        Current = await guard.ExecuteAsync(ct => cart.MergeAsync(guestToken, ct), cancellationToken);
        await session.SetCartTokenAsync(null, cancellationToken);
        Changed?.Invoke();
    }

    /// <summary>Forgets the in-memory cart on sign-out; the account's cart stays on the server.</summary>
    public void Clear()
    {
        Current = null;
        Changed?.Invoke();
    }

    private async Task<CartDto> ApplyAsync(Func<CancellationToken, Task<CartDto>> call, CancellationToken cancellationToken)
    {
        var dto = await guard.ExecuteAsync(call, cancellationToken);
        Current = dto;

        // Guests get a token back and must replay it; signed-in carts return none, leaving the token untouched.
        if (!string.IsNullOrEmpty(dto.Token))
        {
            await session.SetCartTokenAsync(dto.Token, cancellationToken);
        }

        Changed?.Invoke();
        return dto;
    }
}
