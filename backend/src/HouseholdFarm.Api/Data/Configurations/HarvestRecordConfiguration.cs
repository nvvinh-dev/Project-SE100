using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class HarvestRecordConfiguration : IEntityTypeConfiguration<HarvestRecord>
{
    public void Configure(EntityTypeBuilder<HarvestRecord> builder)
    {
        builder.HasOne(h => h.Season).WithMany().HasForeignKey(h => h.SeasonId);

        builder.Property(h => h.QualityGrade).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.Quantity).HasPrecision(18, 3);

        // BR-GEN-10: lớp bảo vệ cuối chống tạo trùng khi yêu cầu bị gửi lại.
        builder.HasIndex(h => new { h.HouseholdId, h.RequestId }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_harvest_records_quantity", "quantity > 0"));
    }
}
