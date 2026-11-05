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
