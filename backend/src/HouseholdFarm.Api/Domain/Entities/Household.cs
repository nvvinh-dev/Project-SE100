using HouseholdFarm.Api.Domain.Common;

namespace HouseholdFarm.Api.Domain.Entities;

// Không có HouseholdId vì chính nó là hộ; không có CreatedById vì hộ được tạo cùng lúc với tài khoản Chủ hộ khi đăng ký.
public class Household : IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // BR-CAT-02: mã hộ tự sinh, duy nhất toàn hệ thống, không sửa được.
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ProductionAddress { get; set; } = string.Empty;

    public string? ContactInfo { get; set; }

    public HouseholdSettings Settings { get; set; } = new();

    // FR-HH-10: hệ số "1 sào = bao nhiêu m²"; database vẫn chỉ lưu diện tích bằng m².
    public decimal? SaoAreaM2 { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }
}
