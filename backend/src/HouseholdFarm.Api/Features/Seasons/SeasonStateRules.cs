using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Seasons;

/// <summary>
/// Nguồn quy tắc duy nhất về điều kiện trạng thái mùa vụ và quyền mở rộng phạm vi (BR-SEA-10, BR-USR-13).
/// Dùng chung cho <see cref="ISeasonStateGuard"/> và API ô chọn mùa vụ (FR-SEA-11).
/// </summary>
public static class SeasonStateRules
{
    private static readonly SeasonStatus[] StatusPlannedAndInProgress = [SeasonStatus.Planned, SeasonStatus.InProgress];
    private static readonly SeasonStatus[] StatusPlannedOnly = [SeasonStatus.Planned];
    private static readonly SeasonStatus[] StatusInProgressOnly = [SeasonStatus.InProgress];
    private static readonly SeasonStatus[] StatusInProgressAndClosed = [SeasonStatus.InProgress, SeasonStatus.Closed];
    private static readonly SeasonStatus[] StatusAll = [SeasonStatus.Planned, SeasonStatus.InProgress, SeasonStatus.Closed, SeasonStatus.Cancelled];
    private static readonly SeasonStatus[] StatusClosedOnly = [SeasonStatus.Closed];

    /// <summary>
    /// Lấy danh sách trạng thái cho phép và quyền bổ sung mở rộng phạm vi (BR-USR-13) theo loại thao tác.
    /// </summary>
    public static (IReadOnlyCollection<SeasonStatus> AllowedStatuses, ExtraPermission? OutOfScopePermission) GetRule(SeasonOperation operation) =>
        operation switch
        {
            SeasonOperation.EditScheduleAndYield => (StatusPlannedAndInProgress, null),
            SeasonOperation.ChangePlotOrCrop => (StatusPlannedOnly, null),
            SeasonOperation.ManagePlannedSupplies => (StatusPlannedOnly, null),
            SeasonOperation.ManageAssignments => (StatusPlannedAndInProgress, null),
            SeasonOperation.ManageTasks => (StatusPlannedAndInProgress, null),
            SeasonOperation.RecordTaskExecution => (StatusInProgressOnly, null),
            SeasonOperation.StockOut => (StatusInProgressOnly, ExtraPermission.ManageStock),
            SeasonOperation.ManageHarvests => (StatusInProgressOnly, null),
            SeasonOperation.AdjustProduceStock => (StatusInProgressOnly, null),
            SeasonOperation.ManageSales => (StatusInProgressOnly, ExtraPermission.ManageFinance),
            SeasonOperation.RecordPayment => (StatusInProgressAndClosed, ExtraPermission.ManageFinance),
            SeasonOperation.ManageExpenses => (StatusInProgressOnly, ExtraPermission.ManageFinance),
            SeasonOperation.ManageNotes => (StatusAll, null),
            SeasonOperation.GenerateAiSummary => (StatusClosedOnly, ExtraPermission.ManageFinance),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Loại thao tác không hợp lệ."),
        };
}

