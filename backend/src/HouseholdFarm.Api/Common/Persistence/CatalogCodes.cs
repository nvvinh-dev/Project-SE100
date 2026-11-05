using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Common.Persistence;

/// <summary>
/// Sinh mã danh mục theo BR-CAT-01: tiền tố (<c>TD</c> cho thửa, <c>VT</c> cho vật tư) + số thứ tự tối thiểu 3 chữ số.
/// Số tiếp theo = số lớn nhất trong các mã đúng mẫu của hộ + 1; mã người dùng tự đặt khác mẫu thì bỏ qua.
/// Mã có số kế tiếp dài quá cột (ví dụ người dùng tự đặt <c>TD</c> + 18 chữ số 9) cũng coi là khác mẫu, để mã sinh ra
/// luôn vừa cột và hộ vẫn tạo được bản ghi bằng mã tự sinh.
/// </summary>
/// <remarks>
/// Hai yêu cầu tạo cùng lúc có thể sinh trùng một mã. Service lưu trong vòng thử lại: gặp vi phạm ràng buộc duy nhất
/// <c>(household_id, code)</c> với mã tự sinh thì sinh lại; mã người dùng nhập thì báo <c>CODE_ALREADY_USED</c>.
/// </remarks>
public static class CatalogCodes
{
    /// <summary>
    /// Số lần thử lưu với mã tự sinh khi bị trùng do tạo cùng lúc. Mỗi lần thua nghĩa là một yêu cầu khác vừa lưu xong,
    /// nên N yêu cầu cùng lúc cần tối đa N lần thử: 5 lần đủ cho 5 người trong một hộ tạo cùng một lúc.
    /// </summary>
    public const int MaxGenerateAttempts = 5;

    private const int MinDigits = 3;

    /// <summary>Mã tiếp theo, ví dụ <c>TD001</c> khi hộ chưa có mã nào đúng mẫu.</summary>
    /// <param name="codes">Mã đang có của hộ (Global Query Filter đã lọc theo hộ), ví dụ <c>db.Plots.Select(p =&gt; p.Code)</c>.</param>
    /// <param name="maxLength">Độ dài tối đa của cột mã.</param>
    public static async Task<string> NextAsync(IQueryable<string> codes, string prefix, int maxLength, CancellationToken ct)
    {
        var candidates = await codes.Where(c => c.StartsWith(prefix)).ToListAsync(ct);
        return Next(candidates, prefix, maxLength);
    }

    /// <summary>Phần tính toán của <see cref="NextAsync"/>, tách riêng để test không cần database.</summary>
    public static string Next(IEnumerable<string> codes, string prefix, int maxLength)
    {
        // Số chữ số tối đa để mã kế tiếp còn vừa cột; giới hạn 18 để không tràn long.
        var maxDigits = Math.Min(maxLength - prefix.Length, 18);

        var max = codes
            .Select(code => NumberOf(code, prefix, maxDigits))
            .OfType<long>()
            .Where(number => Format(number + 1).Length <= maxDigits)
            .DefaultIfEmpty(0)
            .Max();

        return prefix + Format(max + 1);
    }

    private static string Format(long number) => number.ToString($"D{MinDigits}", CultureInfo.InvariantCulture);

    // Số thứ tự của mã đúng mẫu, hoặc null khi mã khác mẫu (người dùng tự đặt, ví dụ "TD-SAU-NHA").
    private static long? NumberOf(string code, string prefix, int maxDigits)
    {
        if (!code.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var digits = code[prefix.Length..];
        return digits.Length >= MinDigits && digits.Length <= maxDigits && digits.All(char.IsAsciiDigit)
            ? long.Parse(digits, CultureInfo.InvariantCulture)
            : null;
    }
}
