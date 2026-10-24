namespace HouseholdFarm.Api.Common.Responses;

/// <summary>
/// Kết quả một trang của hàm service trả danh sách. <see cref="ResponseEnvelopeFilter"/> đưa <see cref="Items"/>
/// vào <c>data</c>, thông tin trang vào <c>meta</c>. Tạo bằng <see cref="PagedQueryableExtensions.ToPagedResultAsync"/>.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount) : IPagedResult
{
    object IPagedResult.Items => Items;

    PageMeta IPagedResult.Meta => new(Page, PageSize, TotalCount);
}
