using PcMarket.Domain.Common;

namespace PcMarket.Domain.Catalog;

/// <summary>A node in the (self-referencing) product category tree.</summary>
public class Category : AuditableEntity
{
    public Guid? ParentId { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = [];
    public ICollection<Product> Products { get; set; } = [];
}
