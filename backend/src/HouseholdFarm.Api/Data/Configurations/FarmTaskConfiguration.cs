using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class FarmTaskConfiguration : IEntityTypeConfiguration<FarmTask>
{
    public void Configure(EntityTypeBuilder<FarmTask> builder)
    {
        builder.HasOne(t => t.Season).WithMany(s => s.FarmTasks).HasForeignKey(t => t.SeasonId);
        builder.HasOne(t => t.Assignee).WithMany().HasForeignKey(t => t.AssigneeId);

        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.TypeOther).HasMaxLength(200);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(50);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_farm_tasks_planned_duration_minutes",
                "planned_duration_minutes IS NULL OR planned_duration_minutes > 0");
            t.HasCheckConstraint(
                "ck_farm_tasks_actual_duration_minutes",
                "actual_duration_minutes IS NULL OR actual_duration_minutes > 0");
        });
    }
}
