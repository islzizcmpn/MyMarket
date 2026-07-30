using PcMarket.Contracts.Common;

namespace PcMarket.Contracts.Content;

public sealed record BannerDto(Guid Id, string Title, string? Subtitle, string ImageUrl, string? LinkUrl, int SortOrder, bool IsActive);

public sealed record SaveBannerRequest(
    string Title, string? Subtitle, string ImageUrl, string? LinkUrl, int SortOrder, bool IsActive,
    IReadOnlyList<TranslationDto>? Translations = null);

/// <summary>Back-office view of a banner: the canonical English columns plus every stored translation. Distinct
/// from <see cref="BannerDto"/>, which the storefront receives already resolved into one language.</summary>
public sealed record AdminBannerDto(
    Guid Id, string Title, string? Subtitle, string ImageUrl, string? LinkUrl, int SortOrder, bool IsActive,
    IReadOnlyList<TranslationDto> Translations);

public sealed record CmsBlockDto(Guid Id, string Key, string Title, string? Body, bool IsActive);

public sealed record SaveCmsBlockRequest(
    string Key, string Title, string? Body, bool IsActive,
    IReadOnlyList<TranslationDto>? Translations = null);

/// <summary>Back-office view of a CMS block: canonical English columns plus every stored translation.</summary>
public sealed record AdminCmsBlockDto(
    Guid Id, string Key, string Title, string? Body, bool IsActive,
    IReadOnlyList<TranslationDto> Translations);
