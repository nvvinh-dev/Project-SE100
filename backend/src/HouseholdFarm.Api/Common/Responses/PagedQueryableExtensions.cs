using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Common.Responses;

public static class PagedQueryableExtensions
{
    /// <summary>
    /// Đếm tổng số dòng rồi lấy đúng trang được yêu cầu. Truy vấn phải đã sắp xếp (<c>OrderBy</c>),
    /// nếu không thứ tự giữa các trang không ổn định.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PageQuery pageQuery,
        CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((pageQuery.Page - 1) * pageQuery.PageSize)
            .Take(pageQuery.PageSize)
            .ToListAsync(ct);

        return new PagedResult<T>(items, pageQuery.Page, pageQuery.PageSize, totalCount);
    }
}
