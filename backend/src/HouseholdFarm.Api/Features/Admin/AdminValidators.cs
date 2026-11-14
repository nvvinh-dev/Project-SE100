using FluentValidation;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Admin;

public sealed class AdminAccountQueryValidator : AbstractValidator<AdminAccountQuery>
{
    public AdminAccountQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra, không kiểm tra lại ở đây.
        RuleFor(q => q.Sort)
            .Must(sort => sort is null || AdminAccountQuery.SortFields.Contains(sort.TrimStart('-')))
            .WithMessage($"Chỉ sắp xếp được theo: {string.Join(", ", AdminAccountQuery.SortFields)}.");
        // BR-USR-06: danh sách không có tài khoản Quản trị nên không lọc theo vai trò này.
        RuleFor(q => q.Role)
            .Must(role => role is null or UserRole.Owner or UserRole.Member or UserRole.Worker)
            .WithMessage("Vai trò chỉ được là Chủ hộ, Thành viên hộ hoặc Nhân công.");
    }
}

public sealed class ResetAccountPasswordRequestValidator : AbstractValidator<ResetAccountPasswordRequest>
{
    public ResetAccountPasswordRequestValidator()
    {
        RuleFor(r => r.NewPassword).MustBeValidPassword("Vui lòng nhập mật khẩu mới.");
    }
}
