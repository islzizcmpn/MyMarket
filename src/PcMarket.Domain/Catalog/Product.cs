using PcMarket.Domain.Common;

namespace PcMarket.Domain.Catalog;

/// <summary>A catalog product. Concrete purchasable units are its <see cref="Variants"/>.</summary>
public class Product : AuditableEntity
{
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }

    /// <summary>Free-form specification sheet (e.g. CPU, RAM) stored as JSONB.</summary>
    public Dictionary<string, string> Specs { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public Category Category { get; set; } = null!;
    public Brand? Brand { get; set; }
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<ProductImage> Images { get; set; } = [];
}
