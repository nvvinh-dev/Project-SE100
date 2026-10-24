namespace HouseholdFarm.Api.Common.Responses;

/// <summary>
/// Tham số chung của mọi API danh sách. Tham số lọc của module (<c>&lt;Tên&gt;Query</c>) kế thừa lớp này
/// và thêm bộ lọc riêng; khoảng giá trị của trang do <c>PageQueryValidator</c> tự kiểm tra.
/// </summary>
public record PageQuery
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;

    /// <summary>Trang cần lấy, bắt đầu từ 1.</summary>
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>Tên trường camelCase để sắp xếp, thêm <c>-</c> phía trước để sắp giảm dần, ví dụ <c>-startDate</c>.</summary>
    public string? Sort { get; init; }

    /// <summary>Chuỗi tìm theo chữ; trường được tìm do từng module quy định.</summary>
    public string? Search { get; init; }
}
