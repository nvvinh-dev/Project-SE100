using FluentValidation;
using Microsoft.AspNetCore.Identity;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Chính sách mật khẩu D-22: tối thiểu 8 ký tự, ít nhất 1 chữ cái và 1 chữ số. Dùng chung cho đăng ký, đổi mật khẩu,
/// tạo thành viên, đặt lại mật khẩu.
/// </summary>
/// <remarks>
/// Validator báo lỗi theo từng trường; cấu hình của Identity khớp với validator để <c>UserManager</c> không từ chối
/// mật khẩu mà validator đã cho qua.
/// </remarks>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>Mật khẩu theo D-22, mỗi điều kiện thiếu một câu báo lỗi riêng.</summary>
    public static IRuleBuilderOptions<T, string> MustBeValidPassword<T>(this IRuleBuilderInitial<T, string> rule, string emptyMessage) =>
        rule
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(emptyMessage)
            .MinimumLength(MinLength).WithMessage($"Mật khẩu phải có ít nhất {MinLength} ký tự.")
            .Must(password => password.Any(char.IsLetter)).WithMessage("Mật khẩu phải có ít nhất 1 chữ cái.")
            .Must(password => password.Any(char.IsAsciiDigit)).WithMessage("Mật khẩu phải có ít nhất 1 chữ số.");

    // Identity không có điều kiện "có chữ cái"; điều kiện này chỉ nằm ở validator.
    internal static void Apply(PasswordOptions options)
    {
        options.RequiredLength = MinLength;
        options.RequireDigit = true;
        options.RequireLowercase = false;
        options.RequireUppercase = false;
        options.RequireNonAlphanumeric = false;
        options.RequiredUniqueChars = 1;
    }
}
