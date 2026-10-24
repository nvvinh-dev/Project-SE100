using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class AIUsageConfiguration : IEntityTypeConfiguration<AIUsage>
{
    public void Configure(EntityTypeBuilder<AIUsage> builder)
    {
        builder.HasOne(u => u.User).WithMany().HasForeignKey(u => u.UserId);

        builder.Property(u => u.Feature).HasConversion<string>().HasMaxLength(50);

        // Đếm lượt AI theo hộ, theo ngày ở mỗi lần gọi (BR-AI-04).
        builder.HasIndex(u => new { u.HouseholdId, u.UsageDate });
    }
}
