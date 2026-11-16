using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Harvests;

/// <summary>
/// Ghi nhận một lần thu hoạch của mùa vụ đang canh tác (FR-HAR-01).
/// Đơn vị tính tự lấy theo đơn vị sản lượng của cây trồng trong mùa vụ.
/// </summary>
/// <param name="RequestId">Sinh một lần khi mở form, chống tạo trùng khi gửi lại (BR-GEN-10).</param>
public sealed record CreateHarvestRecordRequest(
    Guid RequestId,
    Guid SeasonId,
    DateOnly? HarvestDate,
    QualityGrade? QualityGrade,
    decimal Quantity,
    string? Note);

/// <summary>Điều chỉnh phiếu thu hoạch ghi nhầm (FR-HAR-03, BR-HAR-03).</summary>
/// <param name="Reason">Lý do điều chỉnh, bắt buộc (BR-GEN-06).</param>
/// <param name="Version">Giá trị <c>version</c> nhận được khi đọc phiếu thu hoạch (BR-GEN-11).</param>
public sealed record CorrectHarvestRecordRequest(
    DateOnly? HarvestDate,
    QualityGrade? QualityGrade,
    decimal Quantity,
    string? Note,
    string Reason,
    uint Version);

/// <summary>Hủy phiếu thu hoạch ghi nhầm, bắt buộc lý do (FR-HAR-03, BR-HAR-04, BR-GEN-06).</summary>
public sealed record VoidHarvestRecordRequest(string Reason);

/// <summary>Giao dịch bán đang liên kết với phiếu thu hoạch (chỉ hiển thị cho người có quyền tài chính).</summary>
public sealed record HarvestLinkedSaleItem(
    Guid SaleId,
    DateOnly SaleDate,
    decimal Quantity,
    string? CustomerName,
    SalesChannel? Channel);

/// <summary>Chi tiết một phiếu thu hoạch (FR-HAR-03, FR-HAR-04).</summary>
public sealed record HarvestRecordResponse(
    Guid Id,
    Guid SeasonId,
    string CropName,
    string PlotCode,
    DateOnly SeasonStartDate,
    SeasonStatus SeasonStatus,
    Unit YieldUnit,
    DateOnly HarvestDate,
    QualityGrade QualityGrade,
    decimal Quantity,
    string? Note,
    DateTime? VoidedAt,
    string? VoidReason,
    DateTime CreatedAt,
    uint Version,
    bool CanModify,
    IReadOnlyList<HarvestLinkedSaleItem> LinkedSales);

/// <summary>Một dòng của danh sách phiếu thu hoạch (FR-HAR-04).</summary>
public sealed record HarvestRecordListItem(
    Guid Id,
    Guid SeasonId,
    string CropName,
    string PlotCode,
    DateOnly SeasonStartDate,
    Unit YieldUnit,
    DateOnly HarvestDate,
    QualityGrade QualityGrade,
    decimal Quantity,
    string? Note,
    DateTime? VoidedAt);

/// <summary>Tham số lọc danh sách phiếu thu hoạch (FR-HAR-04); <c>from</c>, <c>to</c> lọc theo ngày thu.</summary>
public sealed record HarvestRecordQuery : PageQuery
{
    public Guid? SeasonId { get; init; }

    public QualityGrade? QualityGrade { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// <summary><c>true</c>: gồm cả phiếu thu hoạch đã hủy.</summary>
    public bool? IncludeVoided { get; init; }

    /// <summary><c>true</c>: chế độ chọn phiếu liên kết giao dịch bán (BR-USR-13, bắt buộc kèm SeasonId và QualityGrade, chỉ trả phiếu chưa hủy).</summary>
    public bool? ForSaleLink { get; init; }
}

/// <summary>
/// Tồn nông sản theo mùa vụ và loại chất lượng (FR-HAR-05, FR-HAR-06).
/// Tồn = Tổng sản lượng thu hoạch − Tổng lượng đã bán − Tổng lượng điều chỉnh giảm.
/// </summary>
/// <param name="SeasonId">Id mùa vụ.</param>
/// <param name="QualityGrade">Loại chất lượng nông sản (Grade1, Grade2, Grade3).</param>
/// <param name="TotalHarvested">Tổng sản lượng thu hoạch chưa Void.</param>
/// <param name="TotalSold">Tổng sản lượng đã bán chưa Void.</param>
/// <param name="TotalAdjusted">Tổng sản lượng điều chỉnh giảm chưa Void.</param>
/// <param name="Quantity">Lượng tồn hiện tại (= TotalHarvested - TotalSold - TotalAdjusted).</param>
public record ProduceStockResponse(
    Guid SeasonId,
    QualityGrade QualityGrade,
    decimal TotalHarvested,
    decimal TotalSold,
    decimal TotalAdjusted,
    decimal Quantity);
