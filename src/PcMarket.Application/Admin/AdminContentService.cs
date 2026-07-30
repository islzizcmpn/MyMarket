using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Audit;
using PcMarket.Application.Abstractions.Persistence;
using PcMarket.Application.Localization;
using PcMarket.Contracts.Content;
using PcMarket.Domain.Common;
using PcMarket.Domain.Content;

namespace PcMarket.Application.Admin;

/// <summary>Back-office CRUD for storefront content: promo banners and named CMS blocks. Unlike the storefront
/// reads, these return the canonical English columns alongside every stored translation, so an editor sees and
/// changes each language explicitly rather than whichever one their browser happened to ask for.</summary>
public sealed class AdminContentService(IApplicationDbContext db, IAuditLogger audit, TranslationWriter translations)
{
    // ---- Banners ----
    public async Task<IReadOnlyList<AdminBannerDto>> ListBannersAsync(CancellationToken ct = default)
    {
        var banners = await db.Banners.OrderBy(b => b.SortOrder)
            .Select(b => new { b.Id, b.Title, b.Subtitle, b.ImageUrl, b.LinkUrl, b.SortOrder, b.IsActive })
            .ToListAsync(ct);

        var byBanner = await translations.ListManyAsync(
            TranslatableEntities.Banner, [.. banners.Select(b => b.Id)], ct);

        return [.. banners.Select(b => new AdminBannerDto(
            b.Id, b.Title, b.Subtitle, b.ImageUrl, b.LinkUrl, b.SortOrder, b.IsActive, [.. byBanner[b.Id]]))];
    }

    public async Task<AdminBannerDto> SaveBannerAsync(Guid? id, SaveBannerRequest req, CancellationToken ct = default)
    {
        var banner = id is null
            ? new Banner { Title = req.Title, ImageUrl = req.ImageUrl }
            : await db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new DomainException("Banner not found.");

        banner.Title = req.Title;
        banner.Subtitle = req.Subtitle;
        banner.ImageUrl = req.ImageUrl;
        banner.LinkUrl = req.LinkUrl;
        banner.SortOrder = req.SortOrder;
        banner.IsActive = req.IsActive;

        if (id is null) db.Banners.Add(banner); else banner.UpdatedAt = DateTimeOffset.UtcNow;

        await translations.ReplaceAsync(TranslatableEntities.Banner, banner.Id, req.Translations, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(id is null ? "banner.create" : "banner.update", "Banner", banner.Id.ToString(), banner.Title, ct);

        var saved = await translations.ListAsync(TranslatableEntities.Banner, banner.Id, ct);
        return new AdminBannerDto(
            banner.Id, banner.Title, banner.Subtitle, banner.ImageUrl, banner.LinkUrl, banner.SortOrder, banner.IsActive, saved);
    }

    public async Task<bool> DeleteBannerAsync(Guid id, CancellationToken ct = default)
    {
        var banner = await db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (banner is null) return false;
        db.Banners.Remove(banner);
        // ContentTranslations has no foreign key to its owner, so its rows have to be cleared explicitly.
        await translations.RemoveAllAsync(TranslatableEntities.Banner, id, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("banner.delete", "Banner", id.ToString(), banner.Title, ct);
        return true;
    }

    // ---- CMS blocks ----
    public async Task<IReadOnlyList<AdminCmsBlockDto>> ListBlocksAsync(CancellationToken ct = default)
    {
        var blocks = await db.CmsBlocks.OrderBy(b => b.Key)
            .Select(b => new { b.Id, b.Key, b.Title, b.Body, b.IsActive })
            .ToListAsync(ct);

        var byBlock = await translations.ListManyAsync(
            TranslatableEntities.CmsBlock, [.. blocks.Select(b => b.Id)], ct);

        return [.. blocks.Select(b => new AdminCmsBlockDto(
            b.Id, b.Key, b.Title, b.Body, b.IsActive, [.. byBlock[b.Id]]))];
    }

    public async Task<AdminCmsBlockDto> SaveBlockAsync(Guid? id, SaveCmsBlockRequest req, CancellationToken ct = default)
    {
        var block = id is null
            ? new CmsBlock { Key = req.Key, Title = req.Title }
            : await db.CmsBlocks.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new DomainException("Block not found.");

        block.Key = req.Key.Trim();
        block.Title = req.Title;
        block.Body = req.Body;
        block.IsActive = req.IsActive;

        if (id is null) db.CmsBlocks.Add(block); else block.UpdatedAt = DateTimeOffset.UtcNow;

        await translations.ReplaceAsync(TranslatableEntities.CmsBlock, block.Id, req.Translations, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(id is null ? "cms.create" : "cms.update", "CmsBlock", block.Id.ToString(), block.Key, ct);

        var saved = await translations.ListAsync(TranslatableEntities.CmsBlock, block.Id, ct);
        return new AdminCmsBlockDto(block.Id, block.Key, block.Title, block.Body, block.IsActive, saved);
    }

    public async Task<bool> DeleteBlockAsync(Guid id, CancellationToken ct = default)
    {
        var block = await db.CmsBlocks.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (block is null) return false;
        db.CmsBlocks.Remove(block);
        await translations.RemoveAllAsync(TranslatableEntities.CmsBlock, id, ct);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("cms.delete", "CmsBlock", id.ToString(), block.Key, ct);
        return true;
    }
}
