using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HouseholdFarm.Api.Common.Persistence;

/// <summary>
/// Chống tạo trùng bản ghi giao dịch khi yêu cầu bị gửi lại (BR-GEN-10): gửi lại cùng <c>request_id</c> thì không tạo mới,
/// trả về bản ghi đã tạo lần trước như một kết quả thành công.
/// </summary>
/// <example>
/// <code>
/// await using var tx = await _db.Database.BeginTransactionAsync(ct);
/// // khóa dòng (nếu thao tác cần khóa) ...
/// if (await _db.Sales.FindByRequestIdAsync(request.RequestId, ct) is { } existing)
/// {
///     return ToResponse(existing);
/// }
///
/// // kiểm tra quy tắc, tạo bản ghi, ghi nhật ký ...
/// if (await _db.SaveChangesOrFindDuplicateAsync(sale, ct) is { } duplicate)
/// {
///     return ToResponse(duplicate);
/// }
/// await tx.CommitAsync(ct);
/// </code>
/// </example>
public static class RequestIdExtensions
{
    /// <summary>
    /// Tìm bản ghi đã tạo bằng <paramref name="requestId"/> trong hộ hiện tại.
    /// </summary>
    /// <remarks>
    /// Thao tác có khóa dòng thì gọi ngay sau bước khóa, trước khi kiểm tra quy tắc: yêu cầu gửi trùng đang chờ khóa phải thấy
    /// bản ghi lần trước vừa lưu, nếu không nó kiểm tra lại trên tồn, công nợ đã đổi và bị từ chối nhầm.
    /// </remarks>
    /// <param name="source">Bảng giao dịch, có thể kèm <c>Include</c> để dựng phản hồi.</param>
    public static Task<TEntity?> FindByRequestIdAsync<TEntity>(
        this IQueryable<TEntity> source,
        Guid requestId,
        CancellationToken ct)
        where TEntity : class, IRequestIdentified =>
        source.SingleOrDefaultAsync(e => e.RequestId == requestId, ct);

    /// <summary>
    /// Lưu thay đổi của hàm tạo giao dịch. Hai yêu cầu trùng chạy song song cùng qua bước tìm thì ràng buộc duy nhất
    /// <c>(household_id, request_id)</c> chặn yêu cầu sau; khi đó trả về bản ghi đã có thay cho lỗi.
    /// </summary>
    /// <returns>
    /// <c>null</c> khi đã lưu. Khác <c>null</c> là bản ghi đã có: mọi thay đổi đang giữ (kể cả nhật ký) đã bị bỏ, giao dịch
    /// database đang mở đã bị hủy, bên gọi trả bản ghi này và không commit.
    /// </returns>
    /// <remarks>
    /// Không dùng trong service được gọi lồng trong giao dịch của service khác, vì hàm hủy cả giao dịch và các thay đổi của bên gọi.
    /// </remarks>
    /// <param name="created">Bản ghi vừa thêm, mang <c>RequestId</c> của yêu cầu.</param>
    public static async Task<TEntity?> SaveChangesOrFindDuplicateAsync<TEntity>(
        this AppDbContext db,
        TEntity created,
        CancellationToken ct)
        where TEntity : class, IRequestIdentified
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException ex) when (IsRequestIdViolation(db, typeof(TEntity), ex))
        {
            // PostgreSQL bỏ cả giao dịch sau một câu lệnh lỗi, nên phải hủy hẳn rồi mới đọc lại được;
            // bỏ các thay đổi đang giữ để lần lưu sau trong cùng yêu cầu không ghi lại chúng.
            db.ChangeTracker.Clear();
            if (db.Database.CurrentTransaction is { } transaction)
            {
                await transaction.RollbackAsync(ct);
            }

            return await db.Set<TEntity>().SingleAsync(e => e.RequestId == created.RequestId, ct);
        }
    }

    // Chỉ nhận vi phạm đúng chỉ mục (household_id, request_id) của bảng này; trùng mã, trùng tên ở bảng khác vẫn là lỗi.
    private static bool IsRequestIdViolation(AppDbContext db, Type entityType, DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            return false;
        }

        var requestIdIndex = db.Model.FindEntityType(entityType)!
            .GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(IRequestIdentified.RequestId)));
        return postgres.ConstraintName == requestIdIndex.GetDatabaseName();
    }
}
