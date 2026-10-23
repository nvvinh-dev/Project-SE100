using System.Globalization;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Common.Time;

public class AppClockTests
{
    [Theory(DisplayName = "Hôm nay tính theo giờ Việt Nam: đổi ngày lúc 17:00 UTC")]
    [InlineData("2026-10-22T16:59:59Z", "2026-10-22")]
    [InlineData("2026-10-22T17:00:00Z", "2026-10-23")]
    public void Today_UsesVietnamTime(string utcNow, string expectedToday)
    {
        var clock = TestClock.At(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture));

        Assert.Equal(DateOnly.Parse(expectedToday, CultureInfo.InvariantCulture), clock.Today);
    }

    [Fact(DisplayName = "UtcNow trả thời điểm hiện tại với Kind = Utc")]
    public void UtcNow_ReturnsUtcKind()
    {
        var now = new DateTimeOffset(2026, 10, 23, 1, 30, 0, TimeSpan.Zero);
        var clock = TestClock.At(now);

        Assert.Equal(now.UtcDateTime, clock.UtcNow);
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }
}
