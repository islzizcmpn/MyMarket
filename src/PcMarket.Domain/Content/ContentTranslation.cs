using PcMarket.Domain.Common;

namespace PcMarket.Domain.Content;

/// <summary>One translated field of one entity, in one language. Kept generic — keyed by entity type, id and
/// field name rather than by a foreign key — so a new translatable entity or a new language costs no schema
/// change. The entity's own column stays the canonical English value and is the last fallback.</summary>
public class ContentTranslation : AuditableEntity
{
    /// <summary>Owning entity's type name, e.g. <c>Category</c>. See <c>TranslatableEntities</c>.</summary>
    public required string EntityType { get; set; }

    public required Guid EntityId { get; set; }

    /// <summary>Property being translated, e.g. <c>Name</c>, <c>Title</c>, <c>Subtitle</c>, <c>Body</c>.</summary>
    public required string Field { get; set; }

    /// <summary>Two-letter language code, e.g. <c>ru</c>.</summary>
    public required string Culture { get; set; }

    public required string Value { get; set; }
}
