using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Contracts.Users;
using PcMarket.Domain.Common;
using PcMarket.Domain.Users;

namespace PcMarket.Application.Users;

/// <summary>CRUD for a user's saved delivery addresses, enforcing single-default and ownership.</summary>
public sealed class AddressService(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<AddressDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await db.Addresses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.City)
            .Select(a => ToDto(a))
            .ToListAsync(cancellationToken);

    public async Task<AddressDto> CreateAsync(Guid userId, CreateAddressRequest request, CancellationToken cancellationToken = default)
    {
        var address = new Address
        {
            UserId = userId,
            Region = request.Region,
            City = request.City,
            Street = request.Street,
            Details = request.Details,
            IsDefault = request.IsDefault
        };

        if (request.IsDefault)
        {
            await ClearDefaultAsync(userId, cancellationToken);
        }

        db.Addresses.Add(address);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(address);
    }

    public async Task<AddressDto?> UpdateAsync(
        Guid userId,
        Guid addressId,
        UpdateAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var address = await db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, cancellationToken);
        if (address is null)
        {
            return null;
        }

        if (request.IsDefault && !address.IsDefault)
        {
            await ClearDefaultAsync(userId, cancellationToken);
        }

        address.Region = request.Region;
        address.City = request.City;
        address.Street = request.Street;
        address.Details = request.Details;
        address.IsDefault = request.IsDefault;
        address.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(address);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default)
    {
        var address = await db.Addresses.FirstOrDefaultAsync(a => a.Id == addressId && a.UserId == userId, cancellationToken);
        if (address is null)
        {
            return false;
        }

        db.Addresses.Remove(address);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ClearDefaultAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Addresses
            .Where(a => a.UserId == userId && a.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), cancellationToken);

    private static AddressDto ToDto(Address a) =>
        new(a.Id, a.Region, a.City, a.Street, a.Details, a.IsDefault);
}
