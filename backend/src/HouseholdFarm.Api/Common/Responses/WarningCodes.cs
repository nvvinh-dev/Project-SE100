namespace HouseholdFarm.Api.Common.Responses;

/// <summary>
/// Mã cảnh báo trả về trong <c>warnings</c>: thao tác vẫn thành công, giao diện hiển thị cảnh báo kèm kết quả.
/// Thêm mã mới thì bổ sung danh mục mã cảnh báo của dự án trước.
/// </summary>
public static class WarningCodes
{
    /// <summary>BR-SUP-08, BR-SEA-09: tồn vật tư (sau thao tác, hoặc dự kiến) thấp hơn ngưỡng tối thiểu.</summary>
    public const string LowStock = "LOW_STOCK";

    /// <summary>BR-SUP-07: tồn vật tư âm, khi hộ bật cho phép âm kho.</summary>
    public const string NegativeStock = "NEGATIVE_STOCK";

    /// <summary>BR-SEA-09: vật tư dự kiến của mùa vụ lớn hơn tồn hiện tại.</summary>
    public const string SupplyShortage = "SUPPLY_SHORTAGE";
}
