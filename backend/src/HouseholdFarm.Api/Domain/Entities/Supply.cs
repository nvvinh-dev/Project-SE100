using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

// Tồn kho không lưu ở đây, tính từ tổng các giao dịch kho chưa Void.
public class Supply : IHouseholdOwned, IAuditable, IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public SupplyGroup Group { get; set; }

    // BR-CAT-05: không đổi được khi vật tư đã có giao dịch kho.
    public Unit Unit { get; set; }

    public decimal MinStockLevel { get; set; }

    // Chỉ để gợi ý khi nhập kho, không dùng để tính chi phí.
    public decimal? ReferencePrice { get; set; }

    // BR-CAT-06: chỉ hệ thống cập nhật theo các quy tắc giá vốn.
    public decimal AverageUnitCost { get; set; }

    public RecordStatus Status { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }
}
