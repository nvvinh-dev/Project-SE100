namespace HouseholdFarm.Api.Features.Households;

/// <summary>Thông tin hộ (FR-HH-01) và cài đặt hộ (FR-HH-08) của người đang đăng nhập; không nhận id hộ từ client.</summary>
public interface IHouseholdService
{
    /// <summary>Xem thông tin hộ: Chủ hộ, Thành viên hộ. Lỗi: <c>FORBIDDEN</c>.</summary>
    Task<HouseholdResponse> GetAsync(CancellationToken ct);

    /// <summary>Sửa thông tin hộ, chỉ Chủ hộ. Lỗi: <c>FORBIDDEN</c>, <c>CONCURRENCY_CONFLICT</c>.</summary>
    Task<HouseholdResponse> UpdateAsync(UpdateHouseholdRequest request, CancellationToken ct);

    /// <summary>Xem cài đặt hộ, chỉ Chủ hộ. Lỗi: <c>FORBIDDEN</c>.</summary>
    Task<HouseholdSettingsResponse> GetSettingsAsync(CancellationToken ct);

    /// <summary>
    /// Đổi cài đặt hộ, chỉ Chủ hộ; công tắc nào đổi thì ghi nhật ký <c>Household.ChangeSettings</c>.
    /// Lỗi: <c>FORBIDDEN</c>, <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<HouseholdSettingsResponse> ChangeSettingsAsync(ChangeHouseholdSettingsRequest request, CancellationToken ct);
}
