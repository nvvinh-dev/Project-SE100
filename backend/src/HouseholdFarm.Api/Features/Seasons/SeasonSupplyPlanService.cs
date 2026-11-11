using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Supplies;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Seasons;

/// <summary>
/// Quản lý vật tư dự kiến cho mùa vụ (FR-SEA-04, BR-SEA-09, BR-SEA-10, BR-GEN-13).
/// </summary>
public sealed class SeasonSupplyPlanService(
    AppDbContext db,
    ICurrentUser currentUser,
    IStockService stockService,
    IWarningCollector warnings) : ISeasonSupplyPlanService
{
    public async Task<IReadOnlyList<SeasonSupplyPlanResponse>> GetListAsync(Guid seasonId, CancellationToken ct)
    {
        // Quyền: chỉ Chủ hộ (FR-SEA-04)
        currentUser.EnsureOwner();

        var seasonExists = await db.Seasons.AnyAsync(s => s.Id == seasonId, ct);
        if (!seasonExists)
        {
            throw AppException.NotFound("mùa vụ");
        }

        var plans = await db.SeasonSupplyPlans
            .AsNoTracking()
            .Include(p => p.Supply)
            .Where(p => p.SeasonId == seasonId)
            .OrderBy(p => p.Supply.Name)
            .ToListAsync(ct);

        if (plans.Count == 0)
        {
            return [];
        }

        var supplyIds = plans.Select(p => p.SupplyId).Distinct().ToList();
        var stocks = (await stockService.GetStockAsync(supplyIds, ct))
            .ToDictionary(s => s.SupplyId);

        var result = new List<SeasonSupplyPlanResponse>(plans.Count);
        foreach (var plan in plans)
        {
            stocks.TryGetValue(plan.SupplyId, out var stock);
            var currentStock = stock?.StockQuantity ?? 0m;
            var minStockLevel = stock?.MinStockLevel ?? 0m;

            var warningCode = EvaluateWarning(plan.PlannedQuantity, currentStock, minStockLevel);

            result.Add(new SeasonSupplyPlanResponse(
                plan.Id,
                plan.SeasonId,
                plan.SupplyId,
                plan.Supply.Name,
                plan.Supply.Unit,
                plan.PlannedQuantity,
                currentStock,
                minStockLevel,
                warningCode,
                plan.Version));
        }

        return result;
    }

    public async Task<SeasonSupplyPlanResponse> CreateAsync(CreateSeasonSupplyPlanRequest request, CancellationToken ct)
    {
        // Quyền: chỉ Chủ hộ
        currentUser.EnsureOwner();

        var season = await db.Seasons.FirstOrDefaultAsync(s => s.Id == request.SeasonId, ct)
            ?? throw AppException.NotFound("mùa vụ");

        // BR-SEA-10: chỉ thao tác khi mùa vụ Planned
        if (season.Status != SeasonStatus.Planned)
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                "Chỉ được khai báo vật tư dự kiến khi mùa vụ đang ở trạng thái Lên kế hoạch.");
        }

        var supply = await db.Supplies.FirstOrDefaultAsync(s => s.Id == request.SupplyId, ct)
            ?? throw AppException.NotFound("vật tư");

        // BR-GEN-13: vật tư phải Active
        if (supply.Status != RecordStatus.Active)
        {
            throw new AppException(
                ErrorCodes.CatalogInactive,
                $"Vật tư '{supply.Name}' đang ngừng sử dụng, không thể thêm vào kế hoạch dự kiến.");
        }

        // Mỗi vật tư một dòng trong mùa vụ
        var duplicate = await db.SeasonSupplyPlans.AnyAsync(
            p => p.SeasonId == request.SeasonId && p.SupplyId == request.SupplyId,
            ct);
        if (duplicate)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                $"Vật tư '{supply.Name}' đã có trong danh sách dự kiến của mùa vụ này.");
        }

        var plan = new SeasonSupplyPlan
        {
            HouseholdId = currentUser.HouseholdId!.Value,
            SeasonId = request.SeasonId,
            SupplyId = request.SupplyId,
            PlannedQuantity = request.PlannedQuantity
        };

        db.SeasonSupplyPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        // BR-SEA-09: kiểm tra cảnh báo tồn kho
        var stockList = await stockService.GetStockAsync([request.SupplyId], ct);
        var stock = stockList.FirstOrDefault();
        var currentStock = stock?.StockQuantity ?? 0m;
        var minStockLevel = stock?.MinStockLevel ?? 0m;

        var warningCode = EvaluateAndCollectWarning(supply.Name, supply.Unit.ToString(), plan.PlannedQuantity, currentStock, minStockLevel);

        return new SeasonSupplyPlanResponse(
            plan.Id,
            plan.SeasonId,
            plan.SupplyId,
            supply.Name,
            supply.Unit,
            plan.PlannedQuantity,
            currentStock,
            minStockLevel,
            warningCode,
            plan.Version);
    }

    public async Task<SeasonSupplyPlanResponse> UpdateAsync(Guid id, UpdateSeasonSupplyPlanRequest request, CancellationToken ct)
    {
        // Quyền: chỉ Chủ hộ
        currentUser.EnsureOwner();

        var plan = await db.SeasonSupplyPlans
            .Include(p => p.Season)
            .Include(p => p.Supply)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw AppException.NotFound("vật tư dự kiến");

        // BR-SEA-10: chỉ thao tác khi mùa vụ Planned
        if (plan.Season.Status != SeasonStatus.Planned)
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                "Chỉ được sửa vật tư dự kiến khi mùa vụ đang ở trạng thái Lên kế hoạch.");
        }

        // BR-GEN-11: kiểm tra phiên bản
        db.Entry(plan).Property(p => p.Version).OriginalValue = request.Version;

        plan.PlannedQuantity = request.PlannedQuantity;
        await db.SaveChangesAsync(ct);

        // BR-SEA-09: kiểm tra cảnh báo tồn kho
        var stockList = await stockService.GetStockAsync([plan.SupplyId], ct);
        var stock = stockList.FirstOrDefault();
        var currentStock = stock?.StockQuantity ?? 0m;
        var minStockLevel = stock?.MinStockLevel ?? 0m;

        var warningCode = EvaluateAndCollectWarning(plan.Supply.Name, plan.Supply.Unit.ToString(), plan.PlannedQuantity, currentStock, minStockLevel);

        return new SeasonSupplyPlanResponse(
            plan.Id,
            plan.SeasonId,
            plan.SupplyId,
            plan.Supply.Name,
            plan.Supply.Unit,
            plan.PlannedQuantity,
            currentStock,
            minStockLevel,
            warningCode,
            plan.Version);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        // Quyền: chỉ Chủ hộ
        currentUser.EnsureOwner();

        var plan = await db.SeasonSupplyPlans
            .Include(p => p.Season)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw AppException.NotFound("vật tư dự kiến");

        // BR-SEA-10: chỉ thao tác khi mùa vụ Planned (bảng này được xóa thật khi mùa vụ còn Planned)
        if (plan.Season.Status != SeasonStatus.Planned)
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                "Chỉ được xóa vật tư dự kiến khi mùa vụ đang ở trạng thái Lên kế hoạch.");
        }

        db.SeasonSupplyPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
    }

    // BR-SEA-09: tính mã cảnh báo
    private static string? EvaluateWarning(decimal plannedQuantity, decimal currentStock, decimal minStockLevel)
    {
        if (plannedQuantity > currentStock)
        {
            return WarningCodes.SupplyShortage;
        }

        if (currentStock - plannedQuantity < minStockLevel)
        {
            return WarningCodes.LowStock;
        }

        return null;
    }

    // BR-SEA-09: tính mã cảnh báo và ghi vào IWarningCollector
    private string? EvaluateAndCollectWarning(
        string supplyName,
        string unit,
        decimal plannedQuantity,
        decimal currentStock,
        decimal minStockLevel)
    {
        if (plannedQuantity > currentStock)
        {
            warnings.Add(
                WarningCodes.SupplyShortage,
                $"Lượng dự kiến của vật tư '{supplyName}' ({plannedQuantity} {unit}) lớn hơn tồn kho hiện tại ({currentStock} {unit}).");
            return WarningCodes.SupplyShortage;
        }

        if (currentStock - plannedQuantity < minStockLevel)
        {
            warnings.Add(
                WarningCodes.LowStock,
                $"Tồn kho dự kiến của vật tư '{supplyName}' sau khi dùng còn {currentStock - plannedQuantity} {unit}, thấp hơn ngưỡng tối thiểu ({minStockLevel} {unit}).");
            return WarningCodes.LowStock;
        }

        return null;
    }
}
