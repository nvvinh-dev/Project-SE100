using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Expenses;

/// <summary>
/// Chi phí ghi tay của hộ, do Chủ hộ, Thành viên hộ có quyền Quản lý tài chính thực hiện. Chi phí vật tư không ghi ở đây
/// mà tính từ giá trị xuất kho.
/// </summary>
public interface IExpenseService
{
    /// <summary>
    /// Danh sách chi phí, mới nhất trước; mặc định không gồm khoản chi đã hủy. Lọc thời gian theo <c>expense_date</c>.
    /// Lỗi: <c>FORBIDDEN</c>.
    /// </summary>
    Task<PagedResult<ExpenseListItem>> GetListAsync(ExpenseQuery query, CancellationToken ct);

    /// <summary>Chi tiết một khoản chi. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<ExpenseResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Ghi khoản chi gắn mùa vụ hoặc chi phí chung của hộ. Gửi lại cùng <c>requestId</c> thì trả về khoản chi đã tạo (BR-GEN-10).
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>SEASON_INVALID_STATE</c>.
    /// </summary>
    Task<ExpenseResponse> CreateAsync(CreateExpenseRequest request, CancellationToken ct);

    /// <summary>
    /// Điều chỉnh khoản chi (BR-EXP-03); đổi mùa vụ thì cả mùa vụ cũ và mới đều phải cho phép (BR-EXP-02).
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>ALREADY_VOIDED</c>, <c>SEASON_INVALID_STATE</c>, <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<ExpenseResponse> CorrectAsync(Guid id, CorrectExpenseRequest request, CancellationToken ct);

    /// <summary>
    /// Hủy khoản chi ghi nhầm (BR-GEN-09). Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>ALREADY_VOIDED</c>,
    /// <c>SEASON_INVALID_STATE</c>.
    /// </summary>
    Task<ExpenseResponse> VoidAsync(Guid id, VoidExpenseRequest request, CancellationToken ct);
}
