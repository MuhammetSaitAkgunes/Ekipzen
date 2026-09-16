namespace Ekipzen.Application.Common.Models;

/// <summary>Server-side sayfalanmış liste zarfı (liste uçlarının ortak yanıt şekli).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
