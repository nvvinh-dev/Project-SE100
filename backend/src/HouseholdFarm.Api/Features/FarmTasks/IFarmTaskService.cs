using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.FarmTasks;

public interface IFarmTaskService
{
    /// <summary>Lấy danh sách công việc theo phạm vi làm việc và bộ lọc (FR-TSK-09, FR-TSK-10).</summary>
    Task<PagedResult<FarmTaskListItem>> GetListAsync(FarmTaskQuery query, CancellationToken ct);

    /// <summary>Lấy các nhóm nhắc việc Trễ hạn, Hôm nay, Sắp tới theo giờ Việt Nam (FR-TSK-08, BR-TSK-12).</summary>
    Task<FarmTaskRemindersResponse> GetRemindersAsync(bool mine, CancellationToken ct);

    /// <summary>Lấy danh sách người có thể được giao việc (FR-TSK-01, BR-TSK-02).</summary>
    Task<IReadOnlyList<FarmTaskAssigneeOption>> GetAssigneesAsync(Guid? seasonId, CancellationToken ct);

    /// <summary>Lấy chi tiết một công việc; Nhân công nhận phản hồi rút gọn (FR-TSK-10, BR-USR-14).</summary>
    Task<object> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Tạo công việc đơn lẻ (FR-TSK-01, FR-SEA-05).</summary>
    Task<FarmTaskResponse> CreateAsync(CreateFarmTaskRequest request, CancellationToken ct);

    /// <summary>Tạo nhóm công việc lặp đến hết ngày dự kiến thu hoạch của mùa vụ (FR-TSK-02, BR-TSK-03).</summary>
    Task<IReadOnlyList<FarmTaskResponse>> CreateRecurringAsync(CreateRecurringFarmTasksRequest request, CancellationToken ct);

    /// <summary>Sửa công việc chưa thực hiện (FR-TSK-03, BR-TSK-01).</summary>
    Task<FarmTaskResponse> UpdateAsync(Guid id, UpdateFarmTaskRequest request, CancellationToken ct);

    /// <summary>Hủy một công việc chưa thực hiện (FR-TSK-03, BR-TSK-01).</summary>
    Task<FarmTaskResponse> CancelAsync(Guid id, CancelFarmTaskRequest request, CancellationToken ct);

    /// <summary>Ghi hoặc bổ sung kết quả thực hiện công việc (FR-TSK-04..07, FR-TSK-10).</summary>
    Task<object> RecordResultAsync(Guid id, RecordFarmTaskResultRequest request, CancellationToken ct);

    /// <summary>Hủy toàn bộ công việc chưa thực hiện trong một nhóm lặp (FR-TSK-11, BR-TSK-04).</summary>
    Task<CancelRecurrenceGroupResponse> CancelRecurrenceGroupAsync(Guid recurrenceGroupId, CancellationToken ct);
}
