using PcMarket.Contracts.Users;

namespace PcMarket.ApiClient;

/// <summary>Typed access to the current user's profile and saved addresses (all require authentication).</summary>
public sealed class UsersApiClient(HttpClient http, IApiTokenProvider tokens) : ApiClientBase(http, tokens)
{
    public Task<UserProfileDto?> GetProfileAsync(CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<UserProfileDto>("users/me", cancellationToken);

    public Task<IReadOnlyList<AddressDto>> ListAddressesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<AddressDto>>("users/me/addresses", cancellationToken);

    public Task<AddressDto> CreateAddressAsync(CreateAddressRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CreateAddressRequest, AddressDto>("users/me/addresses", request, cancellationToken);

    public Task<AddressDto?> UpdateAddressAsync(Guid id, UpdateAddressRequest request, CancellationToken cancellationToken = default) =>
        PutOrDefaultAsync<UpdateAddressRequest, AddressDto>($"users/me/addresses/{id}", request, cancellationToken);

    public Task<bool> DeleteAddressAsync(Guid id, CancellationToken cancellationToken = default) =>
        DeleteAsync($"users/me/addresses/{id}", cancellationToken);

    /// <summary>Registers this device for push notifications. Idempotent — safe to call on every launch.</summary>
    public Task RegisterDeviceTokenAsync(RegisterDeviceTokenRequest request, CancellationToken cancellationToken = default) =>
        PostAsync("users/me/device-tokens", request, cancellationToken);

    public Task<bool> DeleteDeviceTokenAsync(string token, CancellationToken cancellationToken = default) =>
        DeleteAsync($"users/me/device-tokens/{Uri.EscapeDataString(token)}", cancellationToken);
}
