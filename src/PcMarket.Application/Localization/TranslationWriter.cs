using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Localization;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Contracts.Common;
using PcMarket.Domain.Content;

namespace PcMarket.Application.Localization;

/// <summary>Back-office side of <c>ContentTranslations</c>: reads the stored values for editing and writes back
/// what the editor submitted. Rows are not tied to their owner by a foreign key (the table is deliberately
/// generic), so deleting an entity must call <see cref="RemoveAllAsync"/> or its translations are orphaned.</summary>
public sealed class TranslationWriter(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<TranslationDto>> ListAsync(
        string entityType, Guid entityId, CancellationToken ct = default) =>
        await db.ContentTranslations
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .OrderBy(t => t.Field).ThenBy(t => t.Culture)
            .Select(t => new TranslationDto(t.Field, t.Culture, t.Value))
            .ToListAsync(ct);

    /// <summary>Translations for many entities at once, so a back-office list does not issue a query per row.</summary>
    public async Task<ILookup<Guid, TranslationDto>> ListManyAsync(
        string entityType, IReadOnlyCollection<Guid> entityIds, CancellationToken ct = default)
    {
        if (entityIds.Count == 0)
        {
            return Array.Empty<(Guid, TranslationDto)>().ToLookup(x => x.Item1, x => x.Item2);
        }

        var rows = await db.ContentTranslations
            .Where(t => t.EntityType == entityType && entityIds.Contains(t.EntityId))
            .OrderBy(t => t.Field).ThenBy(t => t.Culture)
            .Select(t => new { t.EntityId, Dto = new TranslationDto(t.Field, t.Culture, t.Value) })
            .ToListAsync(ct);

        return rows.ToLookup(r => r.EntityId, r => r.Dto);
    }

    /// <summary>Makes the stored translations match <paramref name="submitted"/>: updates changed values, adds new
    /// ones, and removes those the editor blanked out — clearing a field is how you go back to the English
    /// fallback. Ignores unsupported languages and the fallback language itself, which lives on the entity.
    /// Does not save; the caller commits as part of its own unit of work.</summary>
    public async Task ReplaceAsync(
        string entityType,
        Guid entityId,
        IReadOnlyList<TranslationDto>? submitted,
        CancellationToken ct = default)
    {
        if (submitted is null)
        {
            return;
        }

        var wanted = submitted
            .Where(t => !string.IsNullOrWhiteSpace(t.Value))
            .Where(t => t.Culture != LanguageCodes.Fallback && LanguageCodes.IsSupported(t.Culture))
            .GroupBy(t => (t.Field, t.Culture))
            .ToDictionary(g => g.Key, g => g.Last().Value.Trim());

        var existing = await db.ContentTranslations
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .ToListAsync(ct);

        foreach (var row in existing)
        {
            if (wanted.TryGetValue((row.Field, row.Culture), out var value))
            {
                if (row.Value != value)
                {
                    row.Value = value;
                    row.UpdatedAt = DateTimeOffset.UtcNow;
                }

                wanted.Remove((row.Field, row.Culture));
            }
            else
            {
                db.ContentTranslations.Remove(row);
            }
        }

        foreach (var ((field, culture), value) in wanted)
        {
            db.ContentTranslations.Add(new ContentTranslation
            {
                EntityType = entityType,
                EntityId = entityId,
                Field = field,
                Culture = culture,
                Value = value
            });
        }
    }

    /// <summary>Drops every translation for an entity that is being deleted. Does not save.</summary>
    public async Task RemoveAllAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        var rows = await db.ContentTranslations
            .Where(t => t.EntityType == entityType && t.EntityId == entityId)
            .ToListAsync(ct);

        db.ContentTranslations.RemoveRange(rows);
    }
}
