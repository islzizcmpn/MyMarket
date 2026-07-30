using Microsoft.AspNetCore.Identity;
using PcMarket.Application.Abstractions.Identity;
using PcMarket.Application.Users;
using PcMarket.Contracts.Users;
using PcMarket.Infrastructure.Identity;

namespace PcMarket.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/me", async (
            ICurrentUser currentUser,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(currentUser.UserId!.Value.ToString());
            if (user is null)
            {
                return Results.NotFound();
            }

            var roles = await userManager.GetRolesAsync(user);
            return Results.Ok(new UserProfileDto(
                user.Id, user.PhoneNumber, user.Email, user.FullName, roles.ToList(), user.TelegramUserId is not null));
        });

        group.MapGet("/me/addresses", async (
            ICurrentUser currentUser,
            AddressService addresses,
            CancellationToken ct) =>
            Results.Ok(await addresses.ListAsync(currentUser.UserId!.Value, ct)));

        group.MapPost("/me/addresses", async (
                CreateAddressRequest request,
                ICurrentUser currentUser,
                AddressService addresses,
                CancellationToken ct) =>
            {
                var created = await addresses.CreateAsync(currentUser.UserId!.Value, request, ct);
                return Results.Created($"/api/v1/users/me/addresses/{created.Id}", created);
            })
            .WithValidation<CreateAddressRequest>();

        group.MapPut("/me/addresses/{id:guid}", async (
                Guid id,
                UpdateAddressRequest request,
                ICurrentUser currentUser,
                AddressService addresses,
                CancellationToken ct) =>
            {
                var updated = await addresses.UpdateAsync(currentUser.UserId!.Value, id, request, ct);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            })
            .WithValidation<UpdateAddressRequest>();

        group.MapDelete("/me/addresses/{id:guid}", async (
            Guid id,
            ICurrentUser currentUser,
            AddressService addresses,
            CancellationToken ct) =>
            await addresses.DeleteAsync(currentUser.UserId!.Value, id, ct) ? Results.NoContent() : Results.NotFound());

        group.MapPost("/me/device-tokens", async (
                RegisterDeviceTokenRequest request,
                ICurrentUser currentUser,
                DeviceTokenService deviceTokens,
                CancellationToken ct) =>
            {
                await deviceTokens.RegisterAsync(currentUser.UserId!.Value, request, ct);
                return Results.NoContent();
            })
            .WithValidation<RegisterDeviceTokenRequest>();

        group.MapDelete("/me/device-tokens/{token}", async (
            string token,
            ICurrentUser currentUser,
            DeviceTokenService deviceTokens,
            CancellationToken ct) =>
            await deviceTokens.UnregisterAsync(currentUser.UserId!.Value, token, ct)
                ? Results.NoContent()
                : Results.NotFound());
    }
}
