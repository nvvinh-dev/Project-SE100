using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HouseholdFarm.Api.Common.Persistence;

public static class UniqueViolationExtensions
{
    /// <summary>
    /// Lỗi lưu là vi phạm đúng chỉ mục duy nhất gồm các cột <paramref name="propertyNames"/> của <typeparamref name="TEntity"/>,
    /// ví dụ <c>ex.IsUniqueViolation&lt;Plot&gt;(db, nameof(Plot.HouseholdId), nameof(Plot.Code))</c>.
    /// </summary>
    /// <remarks>
    /// Service kiểm tra trùng trước khi lưu để báo lỗi rõ ràng; chỉ mục duy nhất là lớp bảo vệ cuối khi hai yêu cầu chạy cùng lúc.
    /// So đúng tên chỉ mục để vi phạm ở chỉ mục khác vẫn là lỗi thật, không bị báo nhầm thành trùng mã.
    /// </remarks>
    public static bool IsUniqueViolation<TEntity>(this DbUpdateException ex, DbContext db, params string[] propertyNames)
    {
        if (ex.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            return false;
        }

        var index = db.Model.FindEntityType(typeof(TEntity))!
            .GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(propertyNames));
        return postgres.ConstraintName == index.GetDatabaseName();
    }
}
