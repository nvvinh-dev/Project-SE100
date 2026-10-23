using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

// Chỉ cấu hình cột tùy biến; cột có sẵn của Identity giữ cấu hình gốc.
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200);
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(50);
        builder.Property(u => u.Version).IsRowVersion();

        builder.HasOne(u => u.Household).WithMany().HasForeignKey(u => u.HouseholdId);

        builder.ToTable(t =>
        {
            // Quản trị hệ thống là tài khoản duy nhất không thuộc hộ nào.
            t.HasCheckConstraint("ck_asp_net_users_role_household", "(role = 'SystemAdmin') = (household_id IS NULL)");
            // BR-USR-09: quyền bổ sung chỉ bật được cho Thành viên hộ.
            t.HasCheckConstraint(
                "ck_asp_net_users_extra_permissions_member_only",
                "role = 'Member' OR NOT (can_manage_stock OR can_manage_finance OR can_close_season)");
        });
    }
}
