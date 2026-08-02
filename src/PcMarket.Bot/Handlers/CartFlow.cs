using PcMarket.Application.Carts;
using PcMarket.Bot.Presentation;
using PcMarket.Contracts.Cart;

namespace PcMarket.Bot.Handlers;

/// <summary>Cart operations in the chat. Guests get a cart keyed by their Telegram id; once they link, the
/// same <see cref="CartService"/> serves their account cart and the guest cart is merged in.</summary>
public sealed class CartFlow(CartService carts, BotSession session, BotResponder responder)
{
    public async Task ShowCartAsync(BotContext context, CancellationToken cancellationToken = default)
    {
        var (userId, token) = await session.ResolveCartOwnerAsync(context, cancellationToken);
        var cart = await carts.GetCartAsync(userId, token, cancellationToken);
        await RenderAsync(context, cart, cancellationToken);
    }

    public async Task AddAsync(BotContext context, Guid variantId, CancellationToken cancellationToken = default)
    {
        var (userId, token) = await session.ResolveCartOwnerAsync(context, cancellationToken);
        var cart = await carts.AddItemAsync(userId, token, new AddCartItemRequest(variantId, 1), cancellationToken);
        await responder.AcknowledgeAsync(context, BotPhrases.Get(context.Culture, Phrase.AddedToCartToast), cancellationToken);
        await RenderAsync(context, cart, cancellationToken);
    }

    public async Task RemoveAsync(BotContext context, Guid itemId, CancellationToken cancellationToken = default)
    {
        var (userId, token) = await session.ResolveCartOwnerAsync(context, cancellationToken);
        var cart = await carts.RemoveItemAsync(userId, token, itemId, cancellationToken);
        await RenderAsync(context, cart, cancellationToken);
    }

    private Task RenderAsync(BotContext context, CartDto cart, CancellationToken cancellationToken) =>
        responder.ReplyAsync(context, BotText.Cart(context.Culture, cart), BotKeyboards.Cart(context.Culture, cart), cancellationToken);
}
