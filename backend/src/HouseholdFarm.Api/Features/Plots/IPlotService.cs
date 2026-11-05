using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Plots;

/// <summary>Thửa đất của hộ (FR-PLOT-01..03). Thửa không xóa được, chỉ chuyển sang ngừng sử dụng (FR-PLOT-02).</summary>
public interface IPlotService
{
    /// <summary>
    /// Danh sách thửa, lọc theo tình trạng, loại đất, nguồn nước, tìm theo mã và vị trí mô tả.
    /// Chủ hộ, Thành viên hộ thấy mọi thửa của hộ; Nhân công chỉ thấy mã, vị trí thửa của mùa vụ có việc giao cho mình.
    /// </summary>
    Task<PagedResult<PlotListItem>> GetListAsync(PlotQuery query, CancellationToken ct);

    /// <summary>Chi tiết một thửa. Lỗi: <c>NOT_FOUND</c> (thửa của hộ khác, hoặc ngoài phạm vi của Nhân công).</summary>
    Task<PlotResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Tạo thửa, chỉ Chủ hộ. Lỗi: <c>FORBIDDEN</c>, <c>CODE_ALREADY_USED</c>.</summary>
    Task<PlotResponse> CreateAsync(CreatePlotRequest request, CancellationToken ct);

    /// <summary>
    /// Sửa thửa, kể cả đổi tình trạng sử dụng, chỉ Chủ hộ.
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>CODE_ALREADY_USED</c>, <c>CATALOG_IN_USE</c>, <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<PlotResponse> UpdateAsync(Guid id, UpdatePlotRequest request, CancellationToken ct);
}
