namespace PcMarket.Domain.Common;

/// <summary>Canonical role names used for authorization across the system.</summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string Manager = "Manager";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Customer, Manager, Admin];
}
