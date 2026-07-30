using Microsoft.AspNetCore.Mvc;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Carts;
using PcMarket.Contracts.Cart;

namespace PcMarket.Api.Endpoints;

public static class CartEndpoints
{
    private const string CartTokenHeader = "X-Cart-Token";

    public static void MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/cart").WithTags("Cart");

        group.MapGet("/", async (
            CartService cart,
            ICurrentUser currentUser,
            [FromHeader(Name = CartTokenHeader)] string? cartToken,
            CancellationToken ct) =>
            Results.Ok(await cart.GetCartAsync(currentUser.UserId, cartToken, ct)));

        group.MapPost("/items", async (
                AddCartItemRequest request,
                CartService cart,
                ICurrentUser currentUser,
                [FromHeader(Name = CartTokenHeader)] string? cartToken,
                CancellationToken ct) =>
                Results.Ok(await cart.AddItemAsync(currentUser.UserId, cartToken, request, ct)))
            .WithValidation<AddCartItemRequest>();

        group.MapPut("/items/{itemId:guid}", async (
                Guid itemId,
                UpdateCartItemRequest request,
                CartService cart,
                ICurrentUser currentUser,
                [FromHeader(Name = CartTokenHeader)] string? cartToken,
                CancellationToken ct) =>
                Results.Ok(await cart.UpdateItemAsync(currentUser.UserId, cartToken, itemId, request, ct)))
            .WithValidation<UpdateCartItemRequest>();

        group.MapDelete("/items/{itemId:guid}", async (
            Guid itemId,
            CartService cart,
            ICurrentUser currentUser,
            [FromHeader(Name = CartTokenHeader)] string? cartToken,
            CancellationToken ct) =>
            Results.Ok(await cart.RemoveItemAsync(currentUser.UserId, cartToken, itemId, ct)));

        group.MapPost("/merge", async (
                string token,
                CartService cart,
                ICurrentUser currentUser,
                CancellationToken ct) =>
            {
                var userId = currentUser.UserId!.Value;
                return Results.Ok(await cart.MergeAsync(userId, token, ct));
            })
            .RequireAuthorization();
    }
}
