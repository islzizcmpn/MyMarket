namespace PcMarket.Contracts.Common;

/// <summary>A page of results plus paging metadata.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
