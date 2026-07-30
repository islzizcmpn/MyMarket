namespace PcMarket.Application.Abstractions.Catalog;

/// <summary>Full-text product search. Implemented in Infrastructure with PostgreSQL FTS so the
/// Application layer stays free of provider-specific query APIs.</summary>
public interface IProductSearchQuery
{
    /// <summary>Returns matching product ids ordered by relevance, plus the total match count.</summary>
    Task<(IReadOnlyList<Guid> Ids, long Total)> SearchAsync(
        string term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
