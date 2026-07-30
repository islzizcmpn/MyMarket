using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Application.Abstractions.Persistence;

namespace PcMarket.Application.Localization;

/// <summary>Entity-type names used as the discriminator in <c>ContentTranslations</c>.</summary>
public static class TranslatableEntities
{
    public const string Category = nameof(Category);
    public const string Banner = nameof(Banner);
    public const string CmsBlock = nameof(CmsBlock);
}

/// <summary>Translations for one batch of entities, already reduced to the requested language with the
/// English translation as backstop. Resolution order is: requested culture → English translation → the
/// entity's own column (passed in as <c>canonical</c>).</summary>
public sealed class TranslationSet
{
    private readonly Dictionary<(Guid Id, string Field), string> _values;

    internal TranslationSet(Dictionary<(Guid, string), string> values) => _values = values;

    public static TranslationSet Empty { get; } = new([]);

    /// <returns>The translated value, or <paramref name="canonical"/> when nothing is translated.</returns>
    public string Resolve(Guid id, string field, string canonical) =>
        _values.TryGetValue((id, field), out var value) ? value : canonical;

    /// <summary>Nullable overload: a missing translation keeps the canonical value, including when it is null.</summary>
    public string? ResolveOptional(Guid id, string field, string? canonical) =>
        _values.TryGetValue((id, field), out var value) ? value : canonical;
}

/// <summary>Loads translated field values for a batch of entities in one query.</summary>
public sealed class TranslationReader(IApplicationDbContext db, ILanguageContext language)
{
    public async Task<TranslationSet> LoadAsync(
        string entityType,
        IReadOnlyCollection<Guid> entityIds,
        CancellationToken cancellationToken = default)
    {
        var culture = language.Culture;

        // Nothing to look up when the caller wants the language the canonical columns are already in.
        if (entityIds.Count == 0 || culture == LanguageCodes.Fallback)
        {
            return TranslationSet.Empty;
        }

        // Fetch the requested language and English together, then prefer the requested one per field, so a
        // partially translated row falls back field by field rather than all-or-nothing.
        var rows = await db.ContentTranslations
            .Where(t => t.EntityType == entityType
                        && entityIds.Contains(t.EntityId)
                        && (t.Culture == culture || t.Culture == LanguageCodes.Fallback))
            .Select(t => new { t.EntityId, t.Field, t.Culture, t.Value })
            .ToListAsync(cancellationToken);

        var values = new Dictionary<(Guid, string), string>();
        foreach (var row in rows)
        {
            var key = (row.EntityId, row.Field);
            if (row.Culture == culture || !values.ContainsKey(key))
            {
                values[key] = row.Value;
            }
        }

        return new TranslationSet(values);
    }
}
