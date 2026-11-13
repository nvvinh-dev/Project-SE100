using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Crops;

/// <summary>Danh mục cây trồng của hộ (FR-CROP-01..04). Cây không xóa được, chỉ chuyển sang ngừng sử dụng (FR-CROP-03).</summary>
public interface ICropService
{
    /// <summary>
    /// Danh sách cây, lọc theo nhóm, trạng thái, tìm theo tên. Chủ hộ, Thành viên hộ thấy mọi cây của hộ;
    /// Nhân công chỉ thấy tên cây của mùa vụ có việc giao cho mình.
    /// </summary>
    Task<PagedResult<CropListItem>> GetListAsync(CropQuery query, CancellationToken ct);

    /// <summary>Chi tiết một cây. Lỗi: <c>NOT_FOUND</c> (cây của hộ khác, hoặc ngoài phạm vi của Nhân công).</summary>
    Task<CropResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Tạo cây, chỉ Chủ hộ. Lỗi: <c>FORBIDDEN</c>, <c>NAME_ALREADY_USED</c>.</summary>
    Task<CropResponse> CreateAsync(CreateCropRequest request, CancellationToken ct);

    /// <summary>
    /// Sửa cây, kể cả đổi trạng thái, chỉ Chủ hộ. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>NAME_ALREADY_USED</c>,
    /// <c>UNIT_LOCKED</c>, <c>CATALOG_IN_USE</c>, <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<CropResponse> UpdateAsync(Guid id, UpdateCropRequest request, CancellationToken ct);
}
