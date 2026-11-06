using System.Globalization;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Supplies;

/// <summary>
/// Phần dùng chung của mọi thao tác thay đổi tồn vật tư: khóa dòng <c>supplies</c> và kiểm tra tồn.
/// Cả API giao dịch kho lẫn <see cref="IStockService"/> đều gọi qua đây, để không có đường nào bỏ sót kiểm tra.
/// </summary>
public sealed class StockLedger(AppDbContext db, IWarningCollector warnings)
{
    // Số hiển thị trong câu tiếng Việt: dấu phẩy thập phân, không có số 0 thừa của cột numeric(18,3).
    private static readonly NumberFormatInfo _vietnameseNumber = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
    };

    /// <summary>
    /// Khóa dòng <c>supplies</c> trong giao dịch database đang mở (BR-GEN-12). Gọi trước khi tính tồn; thao tác có mùa vụ thì
    /// khóa dòng <c>seasons</c> trước. Lỗi: <c>NOT_FOUND</c>.
    /// </summary>
    public async Task<Supply> LockSupplyAsync(Guid supplyId, CancellationToken ct)
    {
        // DbContext giữ nguyên giá trị của entity đã theo dõi khi đọc lại, nên bên gọi đã nạp vật tư trước đó thì phải tải lại
        // sau khi có khóa, nếu không sẽ tính trên giá bình quân cũ.
        var wasTracked = db.Supplies.Local.Any(s => s.Id == supplyId);

        var supply = await db.Supplies
            .FromSql($"SELECT *, xmin FROM supplies WHERE id = {supplyId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("vật tư");

        if (wasTracked)
        {
            await db.Entry(supply).ReloadAsync(ct);
        }

        return supply;
    }

    /// <summary>
    /// Hàm kiểm tra dùng chung cho mọi thao tác làm giảm tồn, chạy sau khi đã khóa dòng <c>supplies</c>.
    /// BR-SUP-07: tồn sau thao tác âm mà hộ không bật cho phép âm kho thì từ chối, có bật thì cảnh báo <c>NEGATIVE_STOCK</c>.
    /// BR-SUP-08: còn dưới ngưỡng tối thiểu thì cảnh báo <c>LOW_STOCK</c>. Lỗi: <c>INSUFFICIENT_STOCK</c>.
    /// </summary>
    /// <param name="stockBefore">Tồn trước thao tác.</param>
    /// <param name="decrease">Lượng giảm, số dương.</param>
    /// <param name="action">Động từ cho câu báo lỗi, ví dụ "xuất", "giảm", "hủy".</param>
    public async Task CheckDecreaseAsync(
        Supply supply,
        decimal stockBefore,
        decimal decrease,
        string action,
        CancellationToken ct)
    {
        var stockAfter = stockBefore - decrease;
        var unit = UnitLabel(supply.Unit);

        if (stockAfter < 0)
        {
            var allowNegative = await db.Households
                .Where(h => h.Id == supply.HouseholdId)
                .Select(h => h.Settings.AllowNegativeStock)
                .SingleAsync(ct);

            if (!allowNegative)
            {
                throw new AppException(
                    ErrorCodes.InsufficientStock,
                    $"{supply.Name} chỉ còn {Format(stockBefore)} {unit}, không đủ để {action} {Format(decrease)} {unit}.",
                    new { supplyId = supply.Id, available = stockBefore, requested = decrease });
            }

            warnings.Add(WarningCodes.NegativeStock, $"Tồn {supply.Name} sau thao tác là {Format(stockAfter)} {unit}.");
            return;
        }

        if (stockAfter < supply.MinStockLevel)
        {
            warnings.Add(
                WarningCodes.LowStock,
                $"Tồn {supply.Name} sau thao tác còn {Format(stockAfter)} {unit}, "
                + $"thấp hơn ngưỡng {Format(supply.MinStockLevel)} {unit}.");
        }
    }

    private static string Format(decimal value) => value.ToString("#,##0.###", _vietnameseNumber);

    private static string UnitLabel(Unit unit) => unit switch
    {
        Unit.Kilogram => "kg",
        Unit.Gram => "g",
        Unit.Quintal => "tạ",
        Unit.Ton => "tấn",
        Unit.Liter => "lít",
        Unit.Milliliter => "ml",
        Unit.Bag => "bao",
        Unit.Bottle => "chai",
        Unit.Pack => "gói",
        Unit.Plant => "cây",
        Unit.Fruit => "quả",
        Unit.Bunch => "bó",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };
}
