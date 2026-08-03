namespace PcMarket.Contracts.Users;

/// <param name="Language">The language this user chose to be addressed in, or null while they never chose
/// one — a client that reads null applies its own default rather than assuming one here.</param>
public sealed record UserProfileDto(
    Guid Id,
    string? Phone,
    string? Email,
    string? FullName,
    IReadOnlyList<string> Roles,
    bool TelegramLinked,
    string? Language);

/// <summary>Sets the caller's preferred language. The code is validated against the languages the system
/// actually supports, so an unknown one is rejected rather than stored and silently ignored later.</summary>
public sealed record UpdateLanguageRequest(string Culture);

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
