using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace HouseholdFarm.Api.Domain.Entities;

// UserName và PhoneNumber của Identity cùng lưu số điện thoại đã chuẩn hóa (BR-USR-01).
public class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    // Chỉ để trống với SystemAdmin, vì Quản trị hệ thống không thuộc hộ nào.
    public Guid? HouseholdId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    // Ba quyền bổ sung chỉ được bật với Member (BR-USR-09).
    public bool CanManageStock { get; set; }

    public bool CanManageFinance { get; set; }

    public bool CanCloseSeason { get; set; }

    // Khóa do Chủ hộ hoặc Quản trị thực hiện, không tự hết; khác khóa tạm LockoutEnd của Identity (BR-USR-15).
    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public uint Version { get; set; }

    public Household? Household { get; set; }
}
