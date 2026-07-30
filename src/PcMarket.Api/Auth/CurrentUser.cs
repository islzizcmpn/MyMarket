using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using PcMarket.Application.Abstractions.Identity;

namespace PcMarket.Api.Auth;

/// <summary>Resolves the caller from the current request's authenticated principal.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                        ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
