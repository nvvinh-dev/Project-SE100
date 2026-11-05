using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Common.Persistence;

/// <summary>
/// Sinh mã danh mục theo BR-CAT-01: tiền tố (<c>TD</c> cho thửa, <c>VT</c> cho vật tư) + số thứ tự tối thiểu 3 chữ số.
/// Số tiếp theo = số lớn nhất trong các mã đúng mẫu của hộ + 1; mã người dùng tự đặt khác mẫu thì bỏ qua.
/// </summary>
/// <remarks>
/// Hai yêu cầu tạo cùng lúc có thể sinh trùng một mã. Service lưu trong vòng thử lại: gặp vi phạm ràng buộc duy nhất
/// <c>(household_id, code)</c> với mã tự sinh thì sinh lại; mã người dùng nhập thì báo <c>CODE_ALREADY_USED</c>.
/// </remarks>
public static class CatalogCodes
{
    /// <summary>Số lần thử lưu với mã tự sinh khi bị trùng do tạo cùng lúc.</summary>
    public const int MaxGenerateAttempts = 3;

    private const int MinDigits = 3;

    // Mã tối đa 20 ký tự; 18 chữ số vẫn nằm trong long.
    private const int MaxDigits = 18;

    /// <summary>Mã tiếp theo, ví dụ <c>TD001</c> khi hộ chưa có mã nào đúng mẫu.</summary>
    /// <param name="codes">Mã đang có của hộ (Global Query Filter đã lọc theo hộ), ví dụ <c>db.Plots.Select(p =&gt; p.Code)</c>.</param>
    public static async Task<string> NextAsync(IQueryable<string> codes, string prefix, CancellationToken ct)
    {
        var candidates = await codes.Where(c => c.StartsWith(prefix)).ToListAsync(ct);
        return Next(candidates, prefix);
    }

    /// <summary>Phần tính toán của <see cref="NextAsync"/>, tách riêng để test không cần database.</summary>
    public static string Next(IEnumerable<string> codes, string prefix)
    {
        var max = codes
            .Select(code => NumberOf(code, prefix))
            .OfType<long>()
            .DefaultIfEmpty(0)
            .Max();

        return prefix + (max + 1).ToString($"D{MinDigits}", CultureInfo.InvariantCulture);
    }

    // Số thứ tự của mã đúng mẫu, hoặc null khi mã khác mẫu (người dùng tự đặt, ví dụ "TD-SAU-NHA").
    private static long? NumberOf(string code, string prefix)
    {
        if (!code.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var digits = code[prefix.Length..];
        return digits.Length is >= MinDigits and <= MaxDigits && digits.All(char.IsAsciiDigit)
            ? long.Parse(digits, CultureInfo.InvariantCulture)
            : null;
    }
}
