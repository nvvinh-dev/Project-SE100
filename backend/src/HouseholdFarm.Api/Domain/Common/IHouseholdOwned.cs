namespace HouseholdFarm.Api.Domain.Common;

// Bản ghi thuộc về đúng một hộ. Global Query Filter lọc theo cột này; giá trị luôn lấy từ người đang đăng nhập, không lấy từ client.
public interface IHouseholdOwned
{
    Guid HouseholdId { get; set; }
}
