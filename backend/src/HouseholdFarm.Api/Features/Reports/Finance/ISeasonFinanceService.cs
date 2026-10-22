namespace HouseholdFarm.Api.Features.Reports.Finance;

/// <summary>
/// Hàm đọc dùng chung cho số liệu tài chính theo mùa vụ. Tab Tài chính của mùa vụ, các báo cáo và AI đều lấy
/// số liệu qua đây, không tự tính, để số liệu ở mọi nơi khớp nhau.
/// </summary>
public interface ISeasonFinanceService
{
    /// <summary>
    /// Tính chi phí vật tư, chi phí ghi tay, doanh thu, đã thu, công nợ và lợi nhuận của từng mùa vụ.
    /// Tính cho cả mùa vụ, không cắt theo khoảng ngày (BR-RPT-13); loại mọi bản ghi đã Void (BR-RPT-01).
    /// </summary>
    /// <param name="seasonIds">Id các mùa vụ; id trùng chỉ trả một dòng, danh sách rỗng trả danh sách rỗng.</param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Mỗi mùa vụ một dòng, không đảm bảo thứ tự; bên gọi tra theo <c>SeasonId</c>.</returns>
    /// <remarks>
    /// Tự kiểm tra quyền (BR-RPT-04): chỉ Chủ hộ và Thành viên có quyền Quản lý tài chính được xem, kể cả mùa vụ
    /// ngoài phân công (BR-USR-13). Bên gọi không có quyền thì không trả phần tài chính.
    /// Lỗi: <c>FORBIDDEN</c> khi người dùng không có quyền xem số liệu tài chính;
    /// <c>NOT_FOUND</c> khi có mùa vụ không thuộc hộ hiện tại.
    /// </remarks>
    Task<IReadOnlyList<SeasonFinanceResponse>> GetAsync(IReadOnlyCollection<Guid> seasonIds, CancellationToken ct);
}
