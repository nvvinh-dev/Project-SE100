using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Expenses;

// Enum bắt buộc khai báo nullable: thiếu trường trong JSON thì nhận null và validator báo thiếu.
// Khai báo không nullable thì thiếu trường thành giá trị 0 của enum (Utilities) và được lưu như thật.

/// <summary>Ghi một khoản chi phí ghi tay (FR-EXP-01).</summary>
/// <param name="SeasonId">Để trống nghĩa là chi phí chung của hộ.</param>
/// <param name="CategoryOther">Ghi chú nhóm, chỉ dùng khi <c>category = Other</c>.</param>
/// <param name="RequestId">Sinh một lần khi mở form, chống tạo trùng khi gửi lại (BR-GEN-10).</param>
public sealed record CreateExpenseRequest(
    Guid RequestId,
    Guid? SeasonId,
    ExpenseCategory? Category,
    string? CategoryOther,
    decimal Amount,
    DateOnly? ExpenseDate,
    string? Note);

/// <summary>Điều chỉnh khoản chi ghi nhầm (FR-EXP-02); chỉ sửa được các trường ở BR-EXP-03.</summary>
/// <param name="SeasonId">Để trống nghĩa là chuyển thành chi phí chung của hộ.</param>
/// <param name="Reason">Lý do điều chỉnh, bắt buộc (BR-GEN-06).</param>
/// <param name="Version">Giá trị <c>version</c> nhận được khi đọc khoản chi (BR-GEN-11).</param>
public sealed record CorrectExpenseRequest(
    Guid? SeasonId,
    ExpenseCategory? Category,
    string? CategoryOther,
    decimal Amount,
    DateOnly? ExpenseDate,
    string? Note,
    string Reason,
    uint Version);

/// <summary>Hủy khoản chi ghi nhầm, bắt buộc lý do (BR-GEN-06).</summary>
public sealed record VoidExpenseRequest(string Reason);

/// <summary>Chi tiết một khoản chi.</summary>
/// <param name="SeasonId">Trống nghĩa là chi phí chung của hộ; khi đó các trường mùa vụ cũng trống.</param>
/// <param name="SeasonStatus">Để giao diện biết còn điều chỉnh, hủy được không (BR-EXP-02).</param>
public sealed record ExpenseResponse(
    Guid Id,
    Guid? SeasonId,
    string? CropName,
    string? PlotCode,
    DateOnly? SeasonStartDate,
    SeasonStatus? SeasonStatus,
    ExpenseCategory Category,
    string? CategoryOther,
    decimal Amount,
    DateOnly ExpenseDate,
    string? Note,
    DateTime? VoidedAt,
    string? VoidReason,
    DateTime CreatedAt,
    uint Version);

/// <summary>Một dòng của danh sách chi phí.</summary>
/// <param name="SeasonId">Trống nghĩa là chi phí chung của hộ.</param>
public sealed record ExpenseListItem(
    Guid Id,
    ExpenseCategory Category,
    string? CategoryOther,
    decimal Amount,
    DateOnly ExpenseDate,
    Guid? SeasonId,
    string? CropName,
    string? PlotCode,
    DateOnly? SeasonStartDate,
    string? Note,
    DateTime? VoidedAt);

/// <summary>Tham số của danh sách chi phí; <c>from</c>, <c>to</c> lọc theo ngày phát sinh.</summary>
public sealed record ExpenseQuery : PageQuery
{
    public ExpenseCategory? Category { get; init; }

    public Guid? SeasonId { get; init; }

    /// <summary><c>true</c>: chỉ chi phí chung của hộ (không gắn mùa vụ).</summary>
    public bool? General { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// <summary><c>true</c>: gồm cả khoản chi đã hủy.</summary>
    public bool? IncludeVoided { get; init; }
}
