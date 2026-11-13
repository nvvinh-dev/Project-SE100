using FluentValidation;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Sales;

// Validator chỉ kiểm tra định dạng, không truy vấn database.
public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(r => r.Name).CustomerNameRules();
        RuleFor(r => r.Phone).CustomerPhoneRules();
        RuleFor(r => r.Status).IsInEnum().WithMessage("Tình trạng sử dụng không hợp lệ.");
    }
}

public sealed class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(r => r.Name).CustomerNameRules();
        RuleFor(r => r.Phone).CustomerPhoneRules();
        RuleFor(r => r.Status)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn tình trạng sử dụng.")
            .IsInEnum().WithMessage("Tình trạng sử dụng không hợp lệ.");
    }
}

public sealed class CustomerQueryValidator : AbstractValidator<CustomerQuery>
{
    public CustomerQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra, không kiểm tra lại ở đây.
        RuleFor(q => q.Status).IsInEnum().WithMessage("Tình trạng sử dụng không hợp lệ.");
        RuleFor(q => q.Sort).Null().WithMessage("Danh sách khách hàng luôn sắp theo tên.");
    }
}

internal static class CustomerRuleExtensions
{
    public static void CustomerNameRules<T>(this IRuleBuilderInitial<T, string> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập tên khách hàng.")
            .MaximumLength(200).WithMessage("Tên khách hàng tối đa 200 ký tự.");

    // Số điện thoại không bắt buộc; có thì phải chuẩn hóa được để tìm kiếm theo một định dạng.
    public static void CustomerPhoneRules<T>(this IRuleBuilderInitial<T, string?> rule) =>
        rule.Must(phone => string.IsNullOrWhiteSpace(phone) || PhoneNumbers.Normalize(phone) is not null)
            .WithMessage(PhoneNumbers.InvalidMessage);
}
