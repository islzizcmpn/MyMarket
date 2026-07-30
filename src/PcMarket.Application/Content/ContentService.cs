using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Application.Localization;
using PcMarket.Contracts.Content;
using PcMarket.Domain.Content;

namespace PcMarket.Application.Content;

/// <summary>Public (storefront) reads of published content, resolved into the caller's language. The stored
/// <c>Title</c>/<c>Subtitle</c>/<c>Body</c> columns hold the canonical English text and are the last fallback.</summary>
public sealed class ContentService(IApplicationDbContext db, TranslationReader translations)
{
    public async Task<IReadOnlyList<BannerDto>> GetActiveBannersAsync(CancellationToken ct = default)
    {
        var banners = await db.Banners
            .Where(b => b.IsActive)
            .OrderBy(b => b.SortOrder)
            .Select(b => new BannerDto(b.Id, b.Title, b.Subtitle, b.ImageUrl, b.LinkUrl, b.SortOrder, b.IsActive))
            .ToListAsync(ct);

        var text = await translations.LoadAsync(
            TranslatableEntities.Banner, [.. banners.Select(b => b.Id)], ct);

        return [.. banners.Select(b => b with
        {
            Title = text.Resolve(b.Id, nameof(Banner.Title), b.Title),
            Subtitle = text.ResolveOptional(b.Id, nameof(Banner.Subtitle), b.Subtitle),
        })];
    }

    public async Task<CmsBlockDto?> GetBlockAsync(string key, CancellationToken ct = default)
    {
        var block = await db.CmsBlocks
            .Where(b => b.Key == key && b.IsActive)
            .Select(b => new CmsBlockDto(b.Id, b.Key, b.Title, b.Body, b.IsActive))
            .FirstOrDefaultAsync(ct);

        if (block is null)
        {
            return null;
        }

        var text = await translations.LoadAsync(TranslatableEntities.CmsBlock, [block.Id], ct);

        return block with
        {
            Title = text.Resolve(block.Id, nameof(CmsBlock.Title), block.Title),
            Body = text.ResolveOptional(block.Id, nameof(CmsBlock.Body), block.Body),
        };
    }
}
