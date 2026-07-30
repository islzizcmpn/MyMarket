namespace PcMarket.Application.Abstractions.Identity;

/// <summary>Ambient information about the caller, resolved from the current request's JWT.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    bool IsAuthenticated { get; }

    bool IsInRole(string role);
}
