using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Supplies;

/// <summary>Danh mục vật tư của hộ (FR-SUP-01, FR-SUP-02, FR-SUP-09). Vật tư không xóa được, chỉ chuyển sang ngừng sử dụng.</summary>
public interface ISupplyService
{
    /// <summary>
    /// Danh sách vật tư kèm tồn, lọc theo nhóm, trạng thái, tìm theo mã và tên. <c>belowMin</c> chỉ lấy vật tư dưới ngưỡng,
    /// vật tư tồn âm xếp trước (BR-SUP-12). Chủ hộ, Thành viên hộ; giá chỉ trả cho người có quyền Quản lý kho.
    /// Lỗi: <c>FORBIDDEN</c>.
    /// </summary>
    Task<PagedResult<SupplyListItem>> GetListAsync(SupplyQuery query, CancellationToken ct);

    /// <summary>Chi tiết một vật tư kèm tồn. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<SupplyResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Tạo vật tư, chỉ Chủ hộ. Lỗi: <c>FORBIDDEN</c>, <c>CODE_ALREADY_USED</c>.</summary>
    Task<SupplyResponse> CreateAsync(CreateSupplyRequest request, CancellationToken ct);

    /// <summary>
    /// Sửa vật tư, kể cả đổi trạng thái, chỉ Chủ hộ.
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>CODE_ALREADY_USED</c>, <c>UNIT_LOCKED</c>, <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<SupplyResponse> UpdateAsync(Guid id, UpdateSupplyRequest request, CancellationToken ct);
}
