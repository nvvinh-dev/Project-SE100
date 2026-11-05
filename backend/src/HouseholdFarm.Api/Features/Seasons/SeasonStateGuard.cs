using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Seasons;

/// <summary>
/// Kiểm tra và bảo vệ trạng thái mùa vụ cho các module khác (SUP, TSK, HAR, SAL, EXP):
/// kiểm tra mùa vụ tồn tại trong hộ, thuộc phạm vi người dùng (gồm ngoại lệ quyền Kho, TC ở BR-USR-13),
/// và trạng thái cho phép thao tác theo BR-SEA-10.
/// Hỗ trợ khóa dòng seasons (SELECT ... FOR UPDATE) theo BR-GEN-12.
/// </summary>
public class SeasonStateGuard(AppDbContext db, ICurrentUser currentUser) : ISeasonStateGuard
{
    public async Task<SeasonStateResponse> EnsureStatusAsync(
        Guid seasonId,
        IReadOnlyCollection<SeasonStatus> allowedStatuses,
        ExtraPermission? outOfScopePermission,
        bool forUpdate,
        CancellationToken ct)
    {
        if (forUpdate)
        {
            // BR-GEN-12: bên gọi bắt buộc phải mở giao dịch database trước khi yêu cầu khóa dòng
            if (db.Database.CurrentTransaction is null)
            {
                throw new InvalidOperationException("Bên gọi bắt buộc phải mở giao dịch database trước khi yêu cầu khóa dòng mùa vụ (forUpdate = true).");
            }

            if (currentUser.HouseholdId is { } householdId)
            {
                // BR-GEN-12: khóa dòng seasons trước các bảng khác trong giao dịch và trước khi đọc dữ liệu kiểm tra
                await db.Database.ExecuteSqlAsync(
                    $"SELECT 1 FROM seasons WHERE id = {seasonId} AND household_id = {householdId} FOR UPDATE",
                    ct);
            }
        }

        // BR-GEN-01, BR-GEN-02, BR-USR-13: lọc theo hộ (Global Query Filter) và phạm vi làm việc của người dùng
        var query = db.Seasons.AsNoTracking();
        query = outOfScopePermission.HasValue
            ? query.VisibleTo(currentUser, outOfScopePermission.Value)
            : query.VisibleTo(currentUser);

        var season = await query.FirstOrDefaultAsync(s => s.Id == seasonId, ct)
            ?? throw AppException.NotFound("mùa vụ");

        // BR-SEA-10: trạng thái không cho phép thao tác trả SEASON_INVALID_STATE
        if (!allowedStatuses.Contains(season.Status))
        {
            throw new AppException(
                ErrorCodes.SeasonInvalidState,
                $"Mùa vụ đang ở trạng thái '{FormatStatus(season.Status)}', không thể thực hiện thao tác này.",
                new
                {
                    seasonId = season.Id,
                    currentStatus = season.Status,
                    allowedStatuses,
                });
        }

        return new SeasonStateResponse(
            season.Id,
            season.Status,
            season.StartDate,
            season.ExpectedHarvestDate,
            season.PlotId,
            season.CropId);
    }

    public Task<SeasonStateResponse> EnsureInProgressAsync(
        Guid seasonId,
        ExtraPermission? outOfScopePermission,
        bool forUpdate,
        CancellationToken ct) =>
        EnsureStatusAsync(seasonId, [SeasonStatus.InProgress], outOfScopePermission, forUpdate, ct);

    public Task<SeasonStateResponse> EnsureOperationAllowedAsync(
        Guid seasonId,
        SeasonOperation operation,
        bool forUpdate,
        CancellationToken ct)
    {
        var (allowedStatuses, outOfScopePermission) = SeasonStateRules.GetRule(operation);
        return EnsureStatusAsync(seasonId, allowedStatuses, outOfScopePermission, forUpdate, ct);
    }

    private static string FormatStatus(SeasonStatus status) => status switch
    {
        SeasonStatus.Planned => "Lên kế hoạch",
        SeasonStatus.InProgress => "Đang canh tác",
        SeasonStatus.Closed => "Đã chốt",
        SeasonStatus.Cancelled => "Đã hủy",
        _ => status.ToString(),
    };
}
