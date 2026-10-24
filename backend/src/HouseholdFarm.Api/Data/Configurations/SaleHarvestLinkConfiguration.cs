using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class SaleHarvestLinkConfiguration : IEntityTypeConfiguration<SaleHarvestLink>
{
    public void Configure(EntityTypeBuilder<SaleHarvestLink> builder)
    {
        builder.HasKey(l => new { l.SaleId, l.HarvestRecordId });

        builder.HasOne(l => l.Sale).WithMany(s => s.HarvestLinks).HasForeignKey(l => l.SaleId);
        builder.HasOne(l => l.HarvestRecord).WithMany(h => h.SaleLinks).HasForeignKey(l => l.HarvestRecordId);
    }
}
