namespace HouseholdFarm.Api.Common.Persistence;

/// <summary>
/// Mẫu cho tìm kiếm theo chữ (tham số <c>search</c> của API danh sách), dùng với
/// <c>EF.Functions.ILike(cột, SearchPatterns.Contains(search), SearchPatterns.EscapeCharacter)</c>: không phân biệt hoa thường.
/// </summary>
public static class SearchPatterns
{
    public const string EscapeCharacter = "\\";

    /// <summary>
    /// Mẫu "chứa chuỗi". Ký tự đặc biệt của <c>LIKE</c> (<c>%</c>, <c>_</c>) trong chuỗi người dùng nhập được hiểu đúng nghĩa,
    /// không thành ký tự đại diện.
    /// </summary>
    public static string Contains(string search)
    {
        var escaped = search.Trim()
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter, StringComparison.Ordinal)
            .Replace("%", EscapeCharacter + "%", StringComparison.Ordinal)
            .Replace("_", EscapeCharacter + "_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
