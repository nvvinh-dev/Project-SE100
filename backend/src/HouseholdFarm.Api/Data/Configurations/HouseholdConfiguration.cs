using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.Property(h => h.Code).HasMaxLength(20);
        // BR-CAT-02: mã hộ duy nhất toàn hệ thống.
        builder.HasIndex(h => h.Code).IsUnique();

        builder.Property(h => h.Name).HasMaxLength(200);
        builder.Property(h => h.ContactInfo).HasMaxLength(500);
        builder.Property(h => h.SaoAreaM2).HasPrecision(18, 2);

        builder.OwnsOne(h => h.Settings, settings =>
        {
            // Đặt tên tay hai cột này: quy ước đặt tên tự sinh thêm tiền tố settings_ cho cột của owned type,
            // còn tên cột trong bảng households không có tiền tố.
            settings.Property(s => s.AllowNegativeStock).HasColumnName("allow_negative_stock");
            settings.Property(s => s.AllowOverlappingSeasons).HasColumnName("allow_overlapping_seasons");
        });
        builder.Navigation(h => h.Settings).IsRequired();
    }
}
