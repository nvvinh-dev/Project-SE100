namespace HouseholdFarm.Api.Common.Time;

/// <summary>
/// Nguồn thời gian duy nhất của ứng dụng. Không dùng <c>DateTime.Now</c>, <c>DateTime.UtcNow</c>, <c>DateTime.Today</c>
/// hay tiêm thẳng <see cref="TimeProvider"/> vào service.
/// </summary>
public interface IAppClock
{
    /// <summary>Thời điểm hiện tại, <c>Kind = Utc</c>; dùng cho các cột <c>...At</c>.</summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Ngày hôm nay theo giờ Việt Nam; dùng khi so sánh với ngày nghiệp vụ (trễ hạn, ngày không sau hôm nay, lượt AI theo ngày).
    /// </summary>
    DateOnly Today { get; }

    /// <summary>
    /// Thời điểm UTC lúc 0 giờ của <paramref name="date"/> theo giờ Việt Nam; dùng khi lọc cột <c>...At</c> theo khoảng ngày
    /// người dùng chọn (ví dụ từ ngày a đến ngày b là <c>[StartOfDayUtc(a), StartOfDayUtc(b + 1))</c>).
    /// </summary>
    DateTime StartOfDayUtc(DateOnly date);
}
