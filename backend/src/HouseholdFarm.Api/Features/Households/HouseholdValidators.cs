using FluentValidation;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Auth;

namespace HouseholdFarm.Api.Features.Households;

// Validator chỉ kiểm tra định dạng, không truy vấn database. Trùng số điện thoại, vai trò của người đích: kiểm tra trong service.
public sealed class UpdateHouseholdRequestValidator : AbstractValidator<UpdateHouseholdRequest>
{
    public UpdateHouseholdRequestValidator()
    {
        RuleFor(r => r.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập tên hộ.")
            .MaximumLength(200).WithMessage("Tên hộ tối đa 200 ký tự.");
        RuleFor(r => r.ProductionAddress).NotEmpty().WithMessage("Vui lòng nhập địa chỉ khu sản xuất.");
        RuleFor(r => r.ContactInfo).MaximumLength(500).WithMessage("Thông tin liên hệ tối đa 500 ký tự.");
    }
}

public sealed class ChangeHouseholdSettingsRequestValidator : AbstractValidator<ChangeHouseholdSettingsRequest>
{
    public ChangeHouseholdSettingsRequestValidator()
    {
        // Thiếu công tắc thì báo lỗi, không hiểu là tắt: tránh vô tình tắt cài đặt đang bật.
        RuleFor(r => r.AllowNegativeStock).NotNull().WithMessage("Vui lòng chọn có cho phép âm kho hay không.");
        RuleFor(r => r.AllowOverlappingSeasons)
            .NotNull().WithMessage("Vui lòng chọn có cho phép mùa vụ chồng lấn hay không.");
    }
}

public sealed class CreateMemberRequestValidator : AbstractValidator<CreateMemberRequest>
{
    public CreateMemberRequestValidator()
    {
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Phone).ValidPhone();
        RuleFor(r => r.Role).MemberOrWorker();
        RuleFor(r => r.InitialPassword).MustBeValidPassword("Vui lòng nhập mật khẩu ban đầu.");
    }
}

public sealed class UpdateMemberRequestValidator : AbstractValidator<UpdateMemberRequest>
{
    public UpdateMemberRequestValidator()
    {
        RuleFor(r => r.FullName).ValidFullName();
        RuleFor(r => r.Phone).ValidPhone();
    }
}

public sealed class ChangeMemberRoleRequestValidator : AbstractValidator<ChangeMemberRoleRequest>
{
    public ChangeMemberRoleRequestValidator()
    {
        RuleFor(r => r.Role).MemberOrWorker();
    }
}

public sealed class ResetMemberPasswordRequestValidator : AbstractValidator<ResetMemberPasswordRequest>
{
    public ResetMemberPasswordRequestValidator()
    {
        RuleFor(r => r.NewPassword).MustBeValidPassword("Vui lòng nhập mật khẩu mới.");
    }
}

public sealed class MemberQueryValidator : AbstractValidator<MemberQuery>
{
    public MemberQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra, không kiểm tra lại ở đây.
        RuleFor(q => q.Sort)
            .Must(sort => sort is null || MemberQuery.SortFields.Contains(sort.TrimStart('-')))
            .WithMessage($"Chỉ sắp xếp được theo: {string.Join(", ", MemberQuery.SortFields)}.");
        RuleFor(q => q.Role).IsInEnum().WithMessage("Vai trò không hợp lệ.");
    }
}

internal static class MemberRuleExtensions
{
    // BR-USR-08: Chủ hộ chỉ tạo, đổi sang Thành viên hộ hoặc Nhân công.
    public static IRuleBuilderOptions<T, UserRole?> MemberOrWorker<T>(this IRuleBuilderInitial<T, UserRole?> rule) =>
        rule
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn vai trò.")
            .Must(role => role is UserRole.Member or UserRole.Worker)
            .WithMessage("Vai trò chỉ được là Thành viên hộ hoặc Nhân công.");
}
