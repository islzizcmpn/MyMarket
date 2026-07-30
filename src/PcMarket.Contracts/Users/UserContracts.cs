namespace PcMarket.Contracts.Users;

public sealed record UserProfileDto(
    Guid Id,
    string? Phone,
    string? Email,
    string? FullName,
    IReadOnlyList<string> Roles,
    bool TelegramLinked);

public sealed record AddressDto(
    Guid Id,
    string Region,
    string City,
    string Street,
    string? Details,
    bool IsDefault);

public sealed record CreateAddressRequest(
    string Region,
    string City,
    string Street,
    string? Details,
    bool IsDefault);

/// <summary>Client platform a push token was issued for. Mirrors the domain enum by value.</summary>
public enum DevicePlatform
{
    Android = 0,
    Ios = 1
}

/// <summary>Registers the calling device for push notifications. Safe to repeat — the same token is only
/// ever stored once.</summary>
public sealed record RegisterDeviceTokenRequest(string Token, DevicePlatform Platform);

public sealed record UpdateAddressRequest(
    string Region,
    string City,
    string Street,
    string? Details,
    bool IsDefault);
