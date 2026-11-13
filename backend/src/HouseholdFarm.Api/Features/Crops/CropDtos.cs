using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Crops;

// Enum bắt buộc khai báo nullable: thiếu trường trong JSON thì nhận null và validator báo thiếu.
// Khai báo không nullable thì thiếu trường thành giá trị 0 của enum (Staple, Kilogram, Active) và được lưu như thật.

/// <summary>Tạo cây trồng. Cây mới luôn ở trạng thái <c>Active</c>.</summary>
/// <param name="Name">Tên cây, duy nhất trong hộ (BR-CAT-03).</param>
/// <param name="GroupOther">Chỉ lưu khi <paramref name="Group"/> là <c>Other</c>.</param>
/// <param name="YieldUnit">Đơn vị sản lượng, dùng cho mọi phiếu thu hoạch, tồn nông sản, giao dịch bán của cây (FR-CROP-02).</param>
/// <param name="ReferenceInfo">Thông tin tham khảo do hộ tự nhập.</param>
public sealed record CreateCropRequest(
    string? Name,
    CropGroup? Group,
    string? GroupOther,
    Unit? YieldUnit,
    string? ReferenceInfo);

/// <summary>Sửa cây trồng, kể cả đổi trạng thái (FR-CROP-03). Không có API xóa cây.</summary>
/// <param name="YieldUnit">Không đổi được khi cây đã có phiếu thu hoạch (BR-CAT-05).</param>
/// <param name="Version">Giá trị <c>version</c> nhận được khi đọc cây (BR-GEN-11).</param>
public sealed record UpdateCropRequest(
    string? Name,
    CropGroup? Group,
    string? GroupOther,
    Unit? YieldUnit,
    string? ReferenceInfo,
    RecordStatus? Status,
    uint Version);

/// <summary>Chi tiết một cây trồng.</summary>
/// <remarks>Nhân công chỉ được thấy tên cây (BR-USR-14): với Nhân công, các trường còn lại để trống.</remarks>
/// <param name="YieldUnitLocked">Cây đã có phiếu thu hoạch (kể cả phiếu đã hủy) nên không đổi được đơn vị (BR-CAT-05).</param>
public sealed record CropResponse(
    Guid Id,
    string Name,
    CropGroup? Group,
    string? GroupOther,
    Unit? YieldUnit,
    string? ReferenceInfo,
    RecordStatus? Status,
    bool? YieldUnitLocked,
    uint? Version);

/// <summary>Một dòng của danh sách cây trồng. Với Nhân công chỉ có tên (BR-USR-14).</summary>
public sealed record CropListItem(
    Guid Id,
    string Name,
    CropGroup? Group,
    Unit? YieldUnit,
    RecordStatus? Status);

/// <summary>
/// Tham số của danh sách cây trồng: <c>search</c> tìm theo tên; sắp xếp theo <see cref="SortFields"/>, mặc định theo tên.
/// </summary>
public sealed record CropQuery : PageQuery
{
    /// <summary>Các trường sắp xếp được, tên camelCase như trong phản hồi.</summary>
    public static readonly IReadOnlyList<string> SortFields = ["name", "createdAt"];

    public CropGroup? Group { get; init; }

    public RecordStatus? Status { get; init; }
}
