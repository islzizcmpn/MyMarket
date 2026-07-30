using PcMarket.Domain.Common;

namespace PcMarket.Domain.Catalog;

/// <summary>A manufacturer/brand a product belongs to.</summary>
public class Brand : AuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? LogoUrl { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}
