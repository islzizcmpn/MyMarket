using Hangfire.Dashboard;
using PcMarket.Domain.Common;

namespace PcMarket.Api.Auth;

/// <summary>Restricts the Hangfire dashboard to authenticated users in the Admin role.</summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true && http.User.IsInRole(Roles.Admin);
    }
}
