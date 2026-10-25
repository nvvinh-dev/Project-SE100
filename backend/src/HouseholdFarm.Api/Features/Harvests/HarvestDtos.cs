using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Harvests;

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
