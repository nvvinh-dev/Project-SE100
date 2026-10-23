namespace HouseholdFarm.Api.Common.Time;

// Xây trên TimeProvider để test thay được ngày giờ; múi giờ đọc từ cấu hình App:TimeZone.
public sealed class AppClock : IAppClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public AppClock(TimeProvider timeProvider, IConfiguration configuration)
    {
        _timeProvider = timeProvider;
        var timeZoneId = configuration["App:TimeZone"]
            ?? throw new InvalidOperationException("Thiếu cấu hình App:TimeZone.");
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    // "Hôm nay" theo giờ Việt Nam: từ 0 giờ đến 7 giờ sáng ở Việt Nam, ngày UTC vẫn là hôm qua.
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone).DateTime);
}
