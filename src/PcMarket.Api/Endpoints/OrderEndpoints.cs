using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Orders;
using PcMarket.Contracts.Orders;

namespace PcMarket.Api.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/orders").WithTags("Orders").RequireAuthorization();

        group.MapPost("/", async (
                CreateOrderRequest request,
                OrderService orders,
                ICurrentUser currentUser,
                CancellationToken ct) =>
            {
                var order = await orders.CreateAsync(currentUser.UserId!.Value, request, ct);
                return Results.Created($"/api/v1/orders/{order.Id}", order);
            })
            .WithValidation<CreateOrderRequest>();

        group.MapGet("/", async (
            OrderService orders,
            ICurrentUser currentUser,
            CancellationToken ct) =>
            Results.Ok(await orders.ListAsync(currentUser.UserId!.Value, ct)));

        group.MapGet("/{id:guid}", async (
            Guid id,
            OrderService orders,
            ICurrentUser currentUser,
            CancellationToken ct) =>
        {
            var order = await orders.GetAsync(currentUser.UserId!.Value, id, ct);
            return order is null ? Results.NotFound() : Results.Ok(order);
        });

        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            OrderService orders,
            ICurrentUser currentUser,
            CancellationToken ct) =>
            Results.Ok(await orders.CancelAsync(currentUser.UserId!.Value, id, ct)));
    }
}
