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

        // BR-GEN-02: thửa và cây của hộ khác hoặc không tồn tại trả NotFound
        var plot = await db.Plots
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlotId, ct)
            ?? throw AppException.NotFound("thửa đất");

        var crop = await db.Crops
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.CropId, ct)
            ?? throw AppException.NotFound("cây trồng");

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

