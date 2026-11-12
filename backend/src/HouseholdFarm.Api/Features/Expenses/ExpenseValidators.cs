using FluentValidation;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Expenses;

// Validator chỉ kiểm tra định dạng, không truy vấn database. Trạng thái mùa vụ, khoản chi đã hủy: kiểm tra trong service.
public sealed class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseRequestValidator(IAppClock clock)
    {
        RuleFor(r => r.RequestId).NotEmpty().WithMessage("Thiếu mã yêu cầu.");
        RuleFor(r => r.Category).ExpenseCategoryRules();
        RuleFor(r => r.CategoryOther).MaximumLength(200).WithMessage("Ghi chú nhóm chi phí tối đa 200 ký tự.");
        RuleFor(r => r.Amount).AmountRules();
        RuleFor(r => r.ExpenseDate).ExpenseDateRules(clock);
    }
}

public sealed class CorrectExpenseRequestValidator : AbstractValidator<CorrectExpenseRequest>
{
    public CorrectExpenseRequestValidator(IAppClock clock)
    {
        RuleFor(r => r.Category).ExpenseCategoryRules();
        RuleFor(r => r.CategoryOther).MaximumLength(200).WithMessage("Ghi chú nhóm chi phí tối đa 200 ký tự.");
        RuleFor(r => r.Amount).AmountRules();
        RuleFor(r => r.ExpenseDate).ExpenseDateRules(clock);
        // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
        RuleFor(r => r.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do điều chỉnh.");
    }
}

public sealed class VoidExpenseRequestValidator : AbstractValidator<VoidExpenseRequest>
{
    public VoidExpenseRequestValidator()
    {
        // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
        RuleFor(r => r.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do hủy.");
    }
}

public sealed class ExpenseQueryValidator : AbstractValidator<ExpenseQuery>
{
    public ExpenseQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra, không kiểm tra lại ở đây.
        RuleFor(q => q.Category).IsInEnum().WithMessage("Nhóm chi phí không hợp lệ.");
        RuleFor(q => q.To)
            .Must((query, to) => query.From is null || to is null || query.From <= to)
            .WithMessage("Ngày kết thúc không được trước ngày bắt đầu.");
        RuleFor(q => q.General)
            .Must((query, general) => general != true || query.SeasonId is null)
            .WithMessage("Chi phí chung của hộ không gắn mùa vụ, không lọc cùng lúc theo mùa vụ được.");
    }
}

internal static class ExpenseRuleExtensions
{
    // BR-EXP-01: chỉ bốn nhóm của ExpenseCategory, không có nhóm vật tư.
    public static void ExpenseCategoryRules<T>(this IRuleBuilderInitial<T, ExpenseCategory?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn nhóm chi phí.")
            .IsInEnum().WithMessage("Nhóm chi phí không hợp lệ.");

    // BR-EXP-01: amount > 0. Cột numeric(18,2): lẻ hơn 2 chữ số bị làm tròn, quá lớn thì tràn cột.
    public static void AmountRules<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Số tiền tối đa 2 chữ số sau dấu phẩy và 16 chữ số phần nguyên.");

    // BR-GEN-05: ngày đã xảy ra không được sau hôm nay (giờ Việt Nam). BR-EXP-02: được trước ngày bắt đầu mùa vụ.
    public static void ExpenseDateRules<T>(this IRuleBuilderInitial<T, DateOnly?> rule, IAppClock clock) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn ngày phát sinh.")
            .Must(date => date <= clock.Today).WithMessage("Ngày phát sinh không được sau hôm nay.");
}
