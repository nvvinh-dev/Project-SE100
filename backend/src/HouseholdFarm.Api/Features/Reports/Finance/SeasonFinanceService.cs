namespace HouseholdFarm.Api.Features.Reports.Finance;

// Lớp tạm để module khác biên dịch và đăng ký DI được trước; logic thật làm ở NOI-04.
public class SeasonFinanceService : ISeasonFinanceService
{
    public Task<IReadOnlyList<SeasonFinanceResponse>> GetAsync(
        IReadOnlyCollection<Guid> seasonIds,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
