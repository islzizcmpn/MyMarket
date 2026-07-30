using PcMarket.Domain.Common;

namespace PcMarket.Domain.Content;

/// <summary>A named, editable content block (e.g. <c>home-intro</c>, <c>footer-note</c>) rendered by key on
/// the storefront.</summary>
public class CmsBlock : AuditableEntity
{
    /// <summary>Stable lookup key used by the storefront.</summary>
    public required string Key { get; set; }

    public required string Title { get; set; }
    public string? Body { get; set; }
    public bool IsActive { get; set; } = true;
}
