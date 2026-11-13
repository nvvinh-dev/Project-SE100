namespace HouseholdFarm.Api.Features.Seasons;

/// <summary>
/// Khai báo và quản lý vật tư dự kiến cho mùa vụ (FR-SEA-04, BR-SEA-09, BR-SEA-10).
/// </summary>
public interface ISeasonSupplyPlanService
{
    /// <summary>
    /// Lấy danh sách vật tư dự kiến của mùa vụ kèm tồn kho và trạng thái cảnh báo.
    /// </summary>
    Task<IReadOnlyList<SeasonSupplyPlanResponse>> GetListAsync(Guid seasonId, CancellationToken ct);

    /// <summary>
    /// Thêm một dòng vật tư dự kiến vào mùa vụ (chỉ khi mùa vụ Planned).
    /// </summary>
    Task<SeasonSupplyPlanResponse> CreateAsync(CreateSeasonSupplyPlanRequest request, CancellationToken ct);

    /// <summary>
    /// Cập nhật lượng vật tư dự kiến (chỉ khi mùa vụ Planned).
    /// </summary>
    Task<SeasonSupplyPlanResponse> UpdateAsync(Guid id, UpdateSeasonSupplyPlanRequest request, CancellationToken ct);

    /// <summary>
    /// Xóa một dòng vật tư dự kiến (chỉ khi mùa vụ Planned).
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
