using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

public class StockTransaction : IHouseholdOwned, IAuditable, IVersioned, IVoidable, IRequestIdentified
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SupplyId { get; set; }

    public StockTransactionType Type { get; set; }

    public DateOnly TransactionDate { get; set; }

    // Có dấu (In dương, Out âm, Adjustment dương hoặc âm) để tồn kho chỉ cần cộng tổng, không phải xét từng loại.
    public decimal Quantity { get; set; }

    // In: giá nhập thực tế; Out, Adjustment: giá vốn bình quân tại thời điểm ghi.
    public decimal UnitCost { get; set; }

    // BR-SUP-10: khôi phục giá vốn bình quân khi hủy giao dịch làm tăng tồn.
    public decimal AverageUnitCostBefore { get; set; }

    // Lưu cố định lúc tạo, luôn không âm.
    public decimal TotalCost { get; set; }

    // Bắt buộc với Out, để trống với loại khác.
    public Guid? SeasonId { get; set; }

    // Bắt buộc với Adjustment.
    public string? Reason { get; set; }

    public string? Note { get; set; }

    // BR-GEN-10: chống tạo trùng khi yêu cầu bị gửi lại. Giao dịch Out sinh từ công việc do backend tự sinh (BR-TSK-08).
    public Guid RequestId { get; set; }

    public DateTime? VoidedAt { get; set; }

    public Guid? VoidedById { get; set; }

    public string? VoidReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }

    public Supply Supply { get; set; } = null!;

    public Season? Season { get; set; }
}
