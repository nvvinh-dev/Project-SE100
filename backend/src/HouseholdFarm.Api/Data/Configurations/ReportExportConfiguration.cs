using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class ReportExportConfiguration : IEntityTypeConfiguration<ReportExport>
{
    public void Configure(EntityTypeBuilder<ReportExport> builder)
    {
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.ExportedById);

        builder.Property(r => r.ReportType).HasConversion<string>().HasMaxLength(50);
        builder.Property(r => r.Format).HasConversion<string>().HasMaxLength(50);
        builder.Property(r => r.Filters).HasColumnType("jsonb");
    }
}
