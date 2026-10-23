namespace HouseholdFarm.Api.Tests.Infrastructure;

// Thời điểm cố định để test chọn được ngày giờ, ví dụ sát nửa đêm giờ Việt Nam.
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
