using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.JsonWebTokens;
using PcMarket.Domain.Common;

namespace PcMarket.Api.Realtime;

/// <summary>Per-customer order-status feed. Each connection joins a group keyed by the caller's user id so
/// the server can push updates to a specific customer across their open tabs/devices.</summary>
[Authorize]
public sealed class OrderStatusHub : Hub
{
    public static string UserGroup(Guid userId) => $"user-{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = ResolveUserId(Context.User);
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId.Value));
        }

        await base.OnConnectedAsync();
    }

    private static Guid? ResolveUserId(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}

/// <summary>Live new-order feed for the back office. Only Admin/Manager connections join the admin group.</summary>
[Authorize(Roles = $"{Roles.Admin},{Roles.Manager}")]
public sealed class AdminOrderHub : Hub
{
    public const string AdminGroup = "admins";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnConnectedAsync();
    }
}
