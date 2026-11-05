using FluentValidation;
using HouseholdFarm.Api.Common.Auth;

namespace HouseholdFarm.Api.Features.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Phone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập số điện thoại.")
            // BR-USR-01: số không chuẩn hóa được thì không thể là số của tài khoản nào.
            .Must(phone => PhoneNumbers.Normalize(phone) is not null).WithMessage(PhoneNumbers.InvalidMessage);

        RuleFor(r => r.Password).NotEmpty().WithMessage("Vui lòng nhập mật khẩu.");
    }
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Phone).ValidPhone();
        RuleFor(r => r.Password).MustBeValidPassword("Vui lòng nhập mật khẩu.");

        RuleFor(r => r.HouseholdName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập tên hộ.")
            .MaximumLength(200).WithMessage("Tên hộ tối đa 200 ký tự.");

        RuleFor(r => r.ProductionAddress).NotEmpty().WithMessage("Vui lòng nhập địa chỉ khu sản xuất.");

        RuleFor(r => r.ContactInfo).MaximumLength(500).WithMessage("Thông tin liên hệ tối đa 500 ký tự.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");
        RuleFor(r => r.NewPassword).MustBeValidPassword("Vui lòng nhập mật khẩu mới.");
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Phone).ValidPhone();
    }
}

internal static class AuthRuleExtensions
{
    public static IRuleBuilderOptions<T, string> ValidFullName<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập họ tên.")
            .MaximumLength(200).WithMessage("Họ tên tối đa 200 ký tự.");

    // BR-USR-01: số phải chuẩn hóa được về 10 chữ số bắt đầu bằng 0.
    public static IRuleBuilderOptions<T, string> ValidPhone<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập số điện thoại.")
            .Must(phone => PhoneNumbers.Normalize(phone) is not null).WithMessage(PhoneNumbers.InvalidMessage);
}
