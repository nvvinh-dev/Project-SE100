using System.Text.Json;
using System.Text.Json.Serialization;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFarm.Api.Tests.Features.FarmTasks;

/// <summary>Tạo nhanh dữ liệu công việc canh tác phục vụ kiểm thử tích hợp.</summary>
public static class FarmTaskTestData
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static async Task<FarmTask> CreateTaskAsync(
        PostgresFixture postgres,
        TestHousehold household,
        Season season,
        DateOnly scheduledDate,
        CancellationToken ct,
        FarmTaskType type = FarmTaskType.Watering,
        string? typeOther = null,
        ApplicationUser? assignee = null,
        FarmTaskStatus status = FarmTaskStatus.Pending,
        Guid? recurrenceGroupId = null,
        DateOnly? performedDate = null,
        ApplicationUser? createdBy = null,
        params (Supply Supply, decimal? PlannedQuantity, decimal? ActualQuantity, Guid? StockTxId)[] supplies)
    {
        var task = new FarmTask
        {
            SeasonId = season.Id,
            AssigneeId = assignee?.Id,
            Type = type,
            TypeOther = typeOther,
            ScheduledDate = scheduledDate,
            PlannedDurationMinutes = 60,
            Status = status,
            PerformedDate = performedDate ?? (status == FarmTaskStatus.Done ? scheduledDate : null),
            CompletedAt = status == FarmTaskStatus.Done ? new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc) : null,
            CancelledAt = status == FarmTaskStatus.Cancelled ? new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc) : null,
            RecurrenceGroupId = recurrenceGroupId,
            SupplyUsages = supplies.Select(s => new FarmTaskSupplyUsage
            {
                SupplyId = s.Supply.Id,
                PlannedQuantity = s.PlannedQuantity,
                ActualQuantity = s.ActualQuantity,
                StockTransactionId = s.StockTxId,
            }).ToList(),
        };

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(createdBy ?? household.Owner));
        db.FarmTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return task;
    }

    public sealed class FixedClockFactory(PostgresFixture postgres, DateTimeOffset utcNow) : ApiFactory(postgres)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IAppClock>(TestClock.At(utcNow));
                services.PostConfigureAll<JwtBearerOptions>(options =>
                {
                    options.TokenValidationParameters.LifetimeValidator =
                        (_, expires, _, _) => expires is null || expires > utcNow.UtcDateTime;
                });
            });
        }
    }
}
