using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

// Global Query Filter của bảng này nằm ở AppDbContext vì household_id để trống được.
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasOne<Household>().WithMany().HasForeignKey(a => a.HouseholdId);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorId);

        builder.Property(a => a.Action).HasMaxLength(100);
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.OldValues).HasColumnType("jsonb");
        builder.Property(a => a.NewValues).HasColumnType("jsonb");

        builder.HasIndex(a => new { a.HouseholdId, a.OccurredAt });
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
