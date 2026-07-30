using PcMarket.Domain.Common;

namespace PcMarket.Domain.Content;

/// <summary>A promotional banner shown on the storefront home page.</summary>
public class Banner : AuditableEntity
{
    public required string Title { get; set; }
    public string? Subtitle { get; set; }
    public required string ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
