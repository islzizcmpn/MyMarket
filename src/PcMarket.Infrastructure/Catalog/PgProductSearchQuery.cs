using Microsoft.EntityFrameworkCore;
using PcMarket.Application.Abstractions.Catalog;
using PcMarket.Infrastructure.Persistence;

namespace PcMarket.Infrastructure.Catalog;

/// <summary>PostgreSQL full-text search over the generated <c>SearchVector</c> column, ranked by
/// <c>ts_rank</c>. Uses <c>websearch_to_tsquery</c> so user input is parsed leniently.</summary>
public sealed class PgProductSearchQuery(PcMarketDbContext db) : IProductSearchQuery
{
    public async Task<(IReadOnlyList<Guid> Ids, long Total)> SearchAsync(
        string term,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var total = await db.Database
            .SqlQuery<long>($"""
                SELECT count(*)::bigint AS "Value"
                FROM "Products"
                WHERE "IsActive"
                  AND "SearchVector" @@ websearch_to_tsquery('simple', {term})
                """)
            .SingleAsync(cancellationToken);

        if (total == 0)
        {
            return ([], 0);
        }

        var offset = (page - 1) * pageSize;
        var ids = await db.Database
            .SqlQuery<Guid>($"""
                SELECT "Id" AS "Value"
                FROM "Products"
                WHERE "IsActive"
                  AND "SearchVector" @@ websearch_to_tsquery('simple', {term})
                ORDER BY ts_rank("SearchVector", websearch_to_tsquery('simple', {term})) DESC, "CreatedAt" DESC
                LIMIT {pageSize} OFFSET {offset}
                """)
            .ToListAsync(cancellationToken);

        return (ids, total);
    }
}
