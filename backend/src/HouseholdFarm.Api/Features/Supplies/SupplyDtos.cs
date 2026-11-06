using System.Text.Json.Serialization;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Supplies;

// Enum bắt buộc khai báo nullable: thiếu trường trong JSON thì nhận null và validator báo thiếu.
// Khai báo không nullable thì thiếu trường thành giá trị 0 của enum (Seed, Kilogram, Active) và được lưu như thật.

/// <summary>Tạo vật tư. Vật tư mới luôn ở trạng thái <c>Active</c>, tồn 0, giá vốn bình quân 0.</summary>
/// <param name="Code">Để trống thì hệ thống tự sinh dạng <c>VT001</c> (BR-CAT-01).</param>
/// <param name="MinStockLevel">Ngưỡng tồn tối thiểu, không gửi thì bằng 0.</param>
/// <param name="ReferencePrice">Giá nhập tham khảo, chỉ để gợi ý khi nhập kho.</param>
public sealed record CreateSupplyRequest(
    string? Code,
    string Name,
    SupplyGroup? Group,
    Unit? Unit,
    decimal MinStockLevel,
    decimal? ReferencePrice,
    string? Note);

/// <summary>
/// Sửa vật tư, kể cả đổi trạng thái. Không có trường tồn và giá vốn bình quân: tồn đổi qua giao dịch kho,
/// giá vốn do hệ thống cập nhật (BR-CAT-06).
/// </summary>
/// <param name="Version">Giá trị <c>version</c> nhận được khi đọc vật tư (BR-GEN-11).</param>
public sealed record UpdateSupplyRequest(
    string Code,
    string Name,
    SupplyGroup? Group,
    Unit? Unit,
    decimal MinStockLevel,
    decimal? ReferencePrice,
    RecordStatus? Status,
    string? Note,
    uint Version);

/// <summary>Chi tiết một vật tư.</summary>
/// <remarks>Thiếu quyền Quản lý kho thì không có trường giá trong phản hồi.</remarks>
/// <param name="UnitLocked">Vật tư đã có giao dịch kho, không đổi được đơn vị (BR-CAT-05).</param>
public sealed record SupplyResponse(
    Guid Id,
    string Code,
    string Name,
    SupplyGroup Group,
    Unit Unit,
    decimal MinStockLevel,
    decimal StockQuantity,
    RecordStatus Status,
    string? Note,
    bool UnitLocked,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? ReferencePrice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? AverageUnitCost,
    uint Version);

/// <summary>Một dòng của danh sách vật tư. Thiếu quyền Quản lý kho thì không có trường giá.</summary>
public sealed record SupplyListItem(
    Guid Id,
    string Code,
    string Name,
    SupplyGroup Group,
    Unit Unit,
    decimal MinStockLevel,
    decimal StockQuantity,
    RecordStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? ReferencePrice,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? AverageUnitCost);

/// <summary>
/// Tham số của danh sách vật tư: <c>search</c> tìm theo mã và tên; sắp xếp theo <see cref="SortFields"/>, mặc định theo mã.
/// </summary>
public sealed record SupplyQuery : PageQuery
{
    /// <summary>Các trường sắp xếp được, tên camelCase như trong phản hồi.</summary>
    public static readonly IReadOnlyList<string> SortFields = ["code", "name", "stockQuantity", "createdAt"];

    public SupplyGroup? Group { get; init; }

    public RecordStatus? Status { get; init; }

    /// <summary><c>true</c>: chỉ vật tư <c>Active</c> có tồn dưới ngưỡng, vật tư tồn âm xếp trước (BR-SUP-12).</summary>
    public bool? BelowMin { get; init; }
}

// Không có giá để bên gọi trả thẳng được cho Thành viên không có quyền Quản lý kho.
public record SupplyStockResponse(Guid SupplyId, decimal StockQuantity, decimal MinStockLevel);

// Quantity giữ dấu như trong database (Out âm), giao diện tự hiển thị giá trị tuyệt đối.
// UnitCost, TotalCost chỉ được đưa ra API cho Chủ hộ và Thành viên có quyền Quản lý kho.
public record StockTransactionResponse(
    Guid Id,
    Guid SupplyId,
    StockTransactionType Type,
    Guid? SeasonId,
    DateOnly TransactionDate,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalCost,
    string? Reason,
    string? Note,
    DateTime? VoidedAt,
    uint Version)
{
    internal static StockTransactionResponse From(StockTransaction t) => new(
        t.Id,
        t.SupplyId,
        t.Type,
        t.SeasonId,
        t.TransactionDate,
        t.Quantity,
        t.UnitCost,
        t.TotalCost,
        t.Reason,
        t.Note,
        t.VoidedAt,
        t.Version);
}

/// <summary>Một dòng của lịch sử giao dịch kho.</summary>
/// <param name="Quantity">Có dấu như trong database.</param>
/// <param name="IsFromFarmTask">Giao dịch xuất sinh từ công việc, chỉ hủy được qua công việc (BR-TSK-11).</param>
public sealed record StockTransactionListItem(
    Guid Id,
    DateOnly TransactionDate,
    Guid SupplyId,
    string SupplyCode,
    string SupplyName,
    Unit Unit,
    StockTransactionType Type,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalCost,
    Guid? SeasonId,
    string? CropName,
    string? PlotCode,
    DateOnly? SeasonStartDate,
    string? Reason,
    string? Note,
    DateTime? VoidedAt,
    string? VoidReason,
    bool IsFromFarmTask);

/// <summary>Tham số của lịch sử giao dịch kho; <c>from</c>, <c>to</c> lọc theo ngày giao dịch.</summary>
public sealed record StockTransactionQuery : PageQuery
{
    public Guid? SupplyId { get; init; }

    public StockTransactionType? Type { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public Guid? SeasonId { get; init; }

    /// <summary><c>true</c>: gồm cả giao dịch đã hủy.</summary>
    public bool? IncludeVoided { get; init; }
}

/// <summary>Hủy giao dịch kho, bắt buộc lý do (BR-GEN-06).</summary>
public sealed record VoidStockTransactionRequest(string Reason);

/// <summary>Tạo giao dịch kho; trường cần có tùy theo <paramref name="Type"/>.</summary>
/// <param name="Quantity"><c>In</c>, <c>Out</c>: số dương. <c>Adjustment</c>: có dấu, tăng dương, giảm âm.</param>
/// <param name="UnitCost">Giá nhập thực tế, bắt buộc với <c>In</c>; loại khác lấy giá vốn bình quân hiện tại.</param>
/// <param name="SeasonId">Bắt buộc với <c>Out</c>, để trống với loại khác.</param>
/// <param name="Reason">Bắt buộc với <c>Adjustment</c>.</param>
/// <param name="RequestId">Sinh một lần khi mở form, chống tạo trùng khi gửi lại (BR-GEN-10).</param>
public sealed record CreateStockTransactionRequest(
    StockTransactionType? Type,
    Guid SupplyId,
    decimal Quantity,
    decimal? UnitCost,
    DateOnly? TransactionDate,
    Guid? SeasonId,
    string? Reason,
    string? Note,
    Guid RequestId);
