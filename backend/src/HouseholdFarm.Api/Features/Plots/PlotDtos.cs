using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Plots;

// Enum bắt buộc khai báo nullable: thiếu trường trong JSON thì nhận null và validator báo thiếu.
// Khai báo không nullable thì thiếu trường thành giá trị 0 của enum (Alluvial, Well, Active) và được lưu như thật.

/// <summary>Tạo thửa đất. Thửa mới luôn ở trạng thái <c>Active</c>.</summary>
/// <param name="Code">Để trống thì hệ thống tự sinh dạng <c>TD001</c> (BR-CAT-01).</param>
/// <param name="AreaM2">Diện tích, luôn bằng m².</param>
/// <param name="SoilTypeOther">Chỉ lưu khi <paramref name="SoilType"/> là <c>Other</c>.</param>
/// <param name="WaterSourceOther">Chỉ lưu khi <paramref name="WaterSource"/> là <c>Other</c>.</param>
public sealed record CreatePlotRequest(
    string? Code,
    decimal AreaM2,
    string? LocationDescription,
    SoilType? SoilType,
    string? SoilTypeOther,
    WaterSource? WaterSource,
    string? WaterSourceOther,
    string? Note);

/// <summary>Sửa thửa đất, kể cả đổi tình trạng sử dụng (FR-PLOT-02). Không có API xóa thửa.</summary>
/// <param name="Version">Giá trị <c>version</c> nhận được khi đọc thửa (BR-GEN-11).</param>
public sealed record UpdatePlotRequest(
    string Code,
    decimal AreaM2,
    string? LocationDescription,
    SoilType? SoilType,
    string? SoilTypeOther,
    WaterSource? WaterSource,
    string? WaterSourceOther,
    RecordStatus? Status,
    string? Note,
    uint Version);

/// <summary>Chi tiết một thửa đất.</summary>
/// <remarks>
/// Nhân công chỉ được thấy mã và vị trí thửa (BR-USR-14): với Nhân công, các trường còn lại để trống.
/// </remarks>
public sealed record PlotResponse(
    Guid Id,
    string Code,
    decimal? AreaM2,
    string? LocationDescription,
    SoilType? SoilType,
    string? SoilTypeOther,
    WaterSource? WaterSource,
    string? WaterSourceOther,
    RecordStatus? Status,
    string? Note,
    uint? Version);

/// <summary>Một dòng của danh sách thửa đất. Với Nhân công chỉ có mã và vị trí (BR-USR-14).</summary>
public sealed record PlotListItem(
    Guid Id,
    string Code,
    decimal? AreaM2,
    string? LocationDescription,
    SoilType? SoilType,
    WaterSource? WaterSource,
    RecordStatus? Status);

/// <summary>
/// Tham số của danh sách thửa đất: <c>search</c> tìm theo mã và vị trí mô tả; sắp xếp theo
/// <see cref="SortFields"/>, mặc định theo mã.
/// </summary>
public sealed record PlotQuery : PageQuery
{
    /// <summary>Các trường sắp xếp được, tên camelCase như trong phản hồi.</summary>
    public static readonly IReadOnlyList<string> SortFields = ["code", "areaM2", "createdAt"];

    public RecordStatus? Status { get; init; }

    public SoilType? SoilType { get; init; }

    public WaterSource? WaterSource { get; init; }
}
