using HouseholdFarm.Api.Common.Time;
using Microsoft.Extensions.Configuration;

namespace HouseholdFarm.Api.Tests.Infrastructure;

// Đồng hồ thật của ứng dụng chạy trên thời điểm cố định, cùng múi giờ với cấu hình App:TimeZone.
public static class TestClock
{
    private static readonly IConfiguration _configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["App:TimeZone"] = "Asia/Ho_Chi_Minh" })
        .Build();

    public static AppClock At(DateTimeOffset utcNow) => new(new FixedTimeProvider(utcNow), _configuration);
}
