using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash).HasMaxLength(200);
        builder.HasIndex(t => t.TokenHash).IsUnique();

        builder.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId);

        // BR-USR-05: mỗi token chỉ được thay bằng đúng một token mới khi làm mới phiên.
        builder.HasOne(t => t.ReplacedBy).WithOne().HasForeignKey<RefreshToken>(t => t.ReplacedById);
    }
}
