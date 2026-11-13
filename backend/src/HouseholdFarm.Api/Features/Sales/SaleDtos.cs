using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Sales;

// Enum bắt buộc khai báo nullable: thiếu trường trong JSON thì nhận null và validator báo thiếu.
// Khai báo không nullable thì thiếu trường thành giá trị 0 của enum (Grade1) và được lưu như thật.

/// <summary>Tạo giao dịch bán nông sản của một mùa vụ, một loại chất lượng (FR-SAL-01).</summary>
/// <param name="RequestId">Sinh một lần khi mở form, chống tạo trùng khi gửi lại (BR-GEN-10).</param>
/// <param name="CustomerId">Có ít nhất một trong hai <c>customerId</c>, <c>channel</c> (BR-SAL-01).</param>
/// <param name="ChannelOther">Ghi chú kênh bán, chỉ dùng khi <c>channel = Other</c>.</param>
/// <param name="Quantity">Theo đơn vị sản lượng của cây trong mùa vụ.</param>
/// <param name="HarvestRecordIds">Phiếu thu hoạch liên kết (không bắt buộc, BR-SAL-05).</param>
public sealed record CreateSaleRequest(
    Guid RequestId,
    Guid? SeasonId,
    Guid? CustomerId,
    SalesChannel? Channel,
    string? ChannelOther,
    QualityGrade? QualityGrade,
    decimal Quantity,
    decimal UnitPrice,
    DateOnly? SaleDate,
    string? Note,
    IReadOnlyList<Guid>? HarvestRecordIds);

/// <summary>Một phiếu thu hoạch đã liên kết với giao dịch bán (FR-SAL-03).</summary>
/// <param name="Quantity">Sản lượng của phiếu, theo đơn vị sản lượng của cây.</param>
public sealed record SaleHarvestLinkItem(Guid HarvestRecordId, DateOnly HarvestDate, decimal Quantity);

/// <summary>Chi tiết một giao dịch bán. Doanh thu, đã thu, công nợ, trạng thái thanh toán là giá trị tính, không lưu.</summary>
/// <param name="YieldUnit">Đơn vị sản lượng của cây, là đơn vị của số lượng bán.</param>
/// <param name="SeasonStatus">Để giao diện biết còn điều chỉnh, hủy, ghi thanh toán được không (mục 4.3).</param>
public sealed record SaleResponse(
    Guid Id,
    Guid SeasonId,
    string CropName,
    string PlotCode,
    DateOnly SeasonStartDate,
    SeasonStatus SeasonStatus,
    Unit YieldUnit,
    Guid? CustomerId,
    string? CustomerName,
    SalesChannel? Channel,
    string? ChannelOther,
    QualityGrade QualityGrade,
    decimal Quantity,
    decimal UnitPrice,
    DateOnly SaleDate,
    string? Note,
    decimal Revenue,
    decimal PaidAmount,
    decimal OutstandingAmount,
    PaymentStatus PaymentStatus,
    IReadOnlyList<SaleHarvestLinkItem> HarvestLinks,
    DateTime? VoidedAt,
    string? VoidReason,
    DateTime CreatedAt,
    uint Version);

/// <summary>Một dòng của danh sách giao dịch bán (FR-SAL-09).</summary>
public sealed record SaleListItem(
    Guid Id,
    DateOnly SaleDate,
    Guid SeasonId,
    string CropName,
    string PlotCode,
    Guid? CustomerId,
    string? CustomerName,
    SalesChannel? Channel,
    string? ChannelOther,
    QualityGrade QualityGrade,
    decimal Quantity,
    Unit YieldUnit,
    decimal Revenue,
    decimal PaidAmount,
    decimal OutstandingAmount,
    PaymentStatus PaymentStatus,
    DateTime? VoidedAt);

/// <summary>Tham số của danh sách giao dịch bán; <c>from</c>, <c>to</c> lọc theo ngày bán.</summary>
public sealed record SaleQuery : PageQuery
{
    public Guid? SeasonId { get; init; }

    public Guid? CustomerId { get; init; }

    public SalesChannel? Channel { get; init; }

    public PaymentStatus? PaymentStatus { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// <summary><c>true</c>: gồm cả giao dịch đã hủy.</summary>
    public bool? IncludeVoided { get; init; }
}
