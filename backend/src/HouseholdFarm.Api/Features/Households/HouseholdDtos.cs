namespace HouseholdFarm.Api.Features.Households;

/// <summary>Thông tin hộ của người đang đăng nhập (FR-HH-01).</summary>
/// <param name="Code">Mã hộ do hệ thống sinh, không sửa được (BR-CAT-02).</param>
public sealed record HouseholdResponse(
    string Code,
    string Name,
    string ProductionAddress,
    string? ContactInfo,
    uint Version);

/// <summary>Sửa thông tin hộ. Không có mã hộ: mã hộ không sửa được.</summary>
/// <param name="Version">Giá trị <c>version</c> nhận được khi đọc hộ (BR-GEN-11).</param>
public sealed record UpdateHouseholdRequest(
    string? Name,
    string? ProductionAddress,
    string? ContactInfo,
    uint Version);

/// <summary>Hai công tắc cài đặt hộ (FR-HH-08).</summary>
/// <param name="Version">Dùng chung <c>version</c> với thông tin hộ vì cùng nằm trong bảng <c>households</c>.</param>
public sealed record HouseholdSettingsResponse(
    bool AllowNegativeStock,
    bool AllowOverlappingSeasons,
    uint Version);

/// <summary>Đổi cài đặt hộ (thao tác <c>ChangeSettings</c>).</summary>
/// <param name="AllowNegativeStock">Cho phép xuất kho khi tồn không đủ (BR-SUP-07).</param>
/// <param name="AllowOverlappingSeasons">Cho phép mùa vụ chồng lấn trên cùng thửa (BR-SEA-04).</param>
public sealed record ChangeHouseholdSettingsRequest(
    bool? AllowNegativeStock,
    bool? AllowOverlappingSeasons,
    uint Version);
