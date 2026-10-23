using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

// Chỉ chi phí ghi tay; chi phí vật tư tính từ giá trị xuất kho để không tính trùng.
public class Expense : IHouseholdOwned, IAuditable, IVersioned, IVoidable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    // Để trống nghĩa là chi phí chung của hộ.
    public Guid? SeasonId { get; set; }

    public ExpenseCategory Category { get; set; }

    public string? CategoryOther { get; set; }

    public decimal Amount { get; set; }

    public DateOnly ExpenseDate { get; set; }

    public string? Note { get; set; }

    // BR-GEN-10: chống tạo trùng khi yêu cầu bị gửi lại.
    public Guid RequestId { get; set; }

    public DateTime? VoidedAt { get; set; }

    public Guid? VoidedById { get; set; }

    public string? VoidReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }

    public Season? Season { get; set; }
}
