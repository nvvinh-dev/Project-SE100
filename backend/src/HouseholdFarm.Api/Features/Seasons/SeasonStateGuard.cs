using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Seasons;

// Lớp tạm để module khác biên dịch và đăng ký DI được trước; logic thật làm ở [NOI-03].
public class SeasonStateGuard : ISeasonStateGuard
{
    public Task<SeasonStateResponse> EnsureStatusAsync(
        Guid seasonId,
        IReadOnlyCollection<SeasonStatus> allowedStatuses,
        ExtraPermission? outOfScopePermission,
        bool forUpdate,
        CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<SeasonStateResponse> EnsureInProgressAsync(
        Guid seasonId,
        ExtraPermission? outOfScopePermission,
        bool forUpdate,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
