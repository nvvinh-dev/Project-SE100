namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Chuẩn hóa số điện thoại (BR-USR-01), dùng chung cho đăng ký, đăng nhập, tạo thành viên, sửa thông tin cá nhân, quản trị.
/// Số luôn được chuẩn hóa trước khi lưu và trước khi so sánh, nên <c>+84 912 345 678</c> và <c>0912345678</c> là một số.
/// </summary>
public static class PhoneNumbers
{
    /// <summary>Thông báo khi số điện thoại không chuẩn hóa được, dùng trong validator.</summary>
    public const string InvalidMessage = "Số điện thoại phải có 10 chữ số, bắt đầu bằng 0 hoặc +84.";

    /// <summary>
    /// Bỏ khoảng trắng, dấu chấm, dấu gạch; đổi tiền tố <c>+84</c>, <c>84</c> thành <c>0</c>.
    /// Trả về số 10 chữ số bắt đầu bằng <c>0</c>, hoặc <c>null</c> nếu không hợp lệ.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var compact = string.Concat(input.Where(c => !char.IsWhiteSpace(c) && c is not '.' and not '-'));

        if (compact.StartsWith("+84", StringComparison.Ordinal))
        {
            compact = "0" + compact[3..];
        }
        // Chỉ đổi 84 khi chuỗi có 11 chữ số, tức là mã quốc gia đứng trước 9 chữ số còn lại của số.
        else if (compact.StartsWith("84", StringComparison.Ordinal) && compact.Length == 11)
        {
            compact = "0" + compact[2..];
        }

        return compact.Length == 10 && compact[0] == '0' && compact.All(char.IsAsciiDigit) ? compact : null;
    }
}
