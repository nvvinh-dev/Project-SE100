using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Seasons;

public interface ISeasonService
{
    Task<PagedResult<SeasonListItem>> GetListAsync(SeasonQuery query, CancellationToken ct);

    Task<IReadOnlyList<SeasonOptionItem>> GetOptionsAsync(SeasonOptionQuery query, CancellationToken ct);

    Task<SeasonResponse> CreateAsync(CreateSeasonRequest request, CancellationToken ct);

    Task<SeasonResponse> UpdateAsync(Guid id, UpdateSeasonRequest request, CancellationToken ct);

    Task<SeasonResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Lấy chi tiết mùa vụ đầy đủ hoặc rút gọn theo quyền người dùng (FR-SEA-12, BR-USR-13).
    /// </summary>
    Task<SeasonDetailResponse> GetDetailAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Bắt đầu mùa vụ: chuyển từ Planned sang InProgress, khóa dòng seasons và kiểm tra thửa bận (FR-SEA-08, BR-SEA-03, BR-GEN-12).
    /// </summary>
    Task<SeasonResponse> StartAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Kiểm tra chồng lấn kế hoạch Lớp 1 (BR-SEA-02): so khoảng ngày với các mùa vụ Planned/InProgress trên cùng thửa.
    /// Bỏ qua khi hộ bật AllowOverlappingSeasons (BR-SEA-04).
    /// </summary>
    Task EnsureNoPlannedOverlapAsync(Guid plotId, DateOnly startDate, DateOnly expectedHarvestDate, Guid? excludeSeasonId, CancellationToken ct);

    /// <summary>
    /// Kiểm tra thửa đang bận Lớp 2 (BR-SEA-03): từ chối nếu thửa có mùa vụ khác đang InProgress khi Start hoặc Reopen.
    /// Bỏ qua khi hộ bật AllowOverlappingSeasons (BR-SEA-04).
    /// </summary>
    Task EnsurePlotNotBusyAsync(Guid plotId, Guid? excludeSeasonId, CancellationToken ct);
}

