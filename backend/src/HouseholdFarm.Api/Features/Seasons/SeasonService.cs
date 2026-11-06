using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Seasons;

public sealed class SeasonService(AppDbContext db, ICurrentUser currentUser) : ISeasonService
{
    public async Task<SeasonResponse> CreateAsync(CreateSeasonRequest request, CancellationToken ct)
    {
        // Ma trận phân quyền: chỉ Chủ hộ có quyền tạo mùa vụ
        currentUser.EnsureOwner();

        // BR-SEA-01, BR-GEN-13: Thửa đất và cây trồng phải tồn tại trong hộ và đang ở trạng thái Active
        var plot = await EnsurePlotActiveAsync(request.PlotId, ct);
        var crop = await EnsureCropActiveAsync(request.CropId, ct);

        var season = new Season
        {
            PlotId = request.PlotId,
            CropId = request.CropId,
            StartDate = request.StartDate,
            ExpectedHarvestDate = request.ExpectedHarvestDate,
            TargetYield = request.TargetYield,
            Status = SeasonStatus.Planned,
        };

        db.Seasons.Add(season);
        await db.SaveChangesAsync(ct);

        return new SeasonResponse(
            season.Id,
            plot.Id,
            plot.Code,
            crop.Id,
            crop.Name,
            crop.YieldUnit,
            season.StartDate,
            season.ExpectedHarvestDate,
            season.TargetYield,
            season.Status,
            season.Version);
    }

    private async Task<Plot> EnsurePlotActiveAsync(Guid plotId, CancellationToken ct)
    {
        var plot = await db.Plots
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == plotId, ct)
            ?? throw AppException.NotFound("thửa đất");

        // BR-SEA-01, BR-GEN-13: Thửa đất phải ở trạng thái Active khi chọn cho mùa vụ
        if (plot.Status != RecordStatus.Active)
        {
            throw new AppException(
                ErrorCodes.CatalogInactive,
                $"Thửa đất '{plot.Code}' đang ngừng sử dụng, không thể chọn cho mùa vụ.",
                new { plotId = plot.Id, plotCode = plot.Code });
        }

        return plot;
    }

    private async Task<Crop> EnsureCropActiveAsync(Guid cropId, CancellationToken ct)
    {
        var crop = await db.Crops
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cropId, ct)
            ?? throw AppException.NotFound("cây trồng");

        // BR-SEA-01, BR-GEN-13: Cây trồng phải ở trạng thái Active khi chọn cho mùa vụ
        if (crop.Status != RecordStatus.Active)
        {
            throw new AppException(
                ErrorCodes.CatalogInactive,
                $"Cây trồng '{crop.Name}' đang ngừng sử dụng, không thể chọn cho mùa vụ.",
                new { cropId = crop.Id, cropName = crop.Name });
        }

        return crop;
    }

    public async Task<SeasonResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var season = await db.Seasons
            .AsNoTracking()
            .VisibleTo(currentUser)
            .Where(s => s.Id == id)
            .Select(s => new SeasonResponse(
                s.Id,
                s.PlotId,
                s.Plot.Code,
                s.CropId,
                s.Crop.Name,
                s.Crop.YieldUnit,
                s.StartDate,
                s.ExpectedHarvestDate,
                s.TargetYield,
                s.Status,
                s.Version))
            .FirstOrDefaultAsync(ct)
            ?? throw AppException.NotFound("mùa vụ");

        return season;
    }
}

