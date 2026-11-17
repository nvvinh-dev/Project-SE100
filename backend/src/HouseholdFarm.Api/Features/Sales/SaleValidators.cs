using FluentValidation;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Sales;

// Validator chỉ kiểm tra định dạng, không truy vấn database. Trạng thái mùa vụ, khách hàng, tồn nông sản,
// ngày bán so với ngày bắt đầu mùa vụ: kiểm tra trong service.
public sealed class CreateSaleRequestValidator : AbstractValidator<CreateSaleRequest>
{
    public CreateSaleRequestValidator(IAppClock clock)
    {
        RuleFor(r => r.RequestId).NotEmpty().WithMessage("Thiếu mã yêu cầu.");
        RuleFor(r => r.SeasonId).NotNull().WithMessage("Vui lòng chọn mùa vụ.");
        RuleFor(r => r.Channel).CustomerOrChannelRules(r => r.CustomerId);
        RuleFor(r => r.ChannelOther).MaximumLength(200).WithMessage("Ghi chú kênh bán tối đa 200 ký tự.");
        RuleFor(r => r.QualityGrade).QualityGradeRules();
        RuleFor(r => r.Quantity).SaleQuantityRules();
        RuleFor(r => r.UnitPrice).UnitPriceRules();
        RuleFor(r => r.SaleDate).SaleDateRules(clock);
    }
}

public sealed class CorrectSaleRequestValidator : AbstractValidator<CorrectSaleRequest>
{
    public CorrectSaleRequestValidator(IAppClock clock)
    {
        // BR-SAL-03: giá trị mới phải thỏa các điều kiện dữ liệu của BR-SAL-01.
        RuleFor(r => r.Channel).CustomerOrChannelRules(r => r.CustomerId);
        RuleFor(r => r.ChannelOther).MaximumLength(200).WithMessage("Ghi chú kênh bán tối đa 200 ký tự.");
        RuleFor(r => r.QualityGrade).QualityGradeRules();
        RuleFor(r => r.Quantity).SaleQuantityRules();
        RuleFor(r => r.UnitPrice).UnitPriceRules();
        RuleFor(r => r.SaleDate).SaleDateRules(clock);
        // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
        RuleFor(r => r.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do điều chỉnh.");
    }
}

public sealed class VoidSaleRequestValidator : AbstractValidator<VoidSaleRequest>
{
    public VoidSaleRequestValidator()
    {
        // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
        RuleFor(r => r.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do hủy.");
    }
}

public sealed class SaleQueryValidator : AbstractValidator<SaleQuery>
{
    public SaleQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra, không kiểm tra lại ở đây.
        RuleFor(q => q.Channel).IsInEnum().WithMessage("Kênh bán không hợp lệ.");
        RuleFor(q => q.PaymentStatus).IsInEnum().WithMessage("Trạng thái thanh toán không hợp lệ.");
        RuleFor(q => q.To)
            .Must((query, to) => query.From is null || to is null || query.From <= to)
            .WithMessage("Ngày kết thúc không được trước ngày bắt đầu.");
        RuleFor(q => q.Sort).Null().WithMessage("Danh sách giao dịch bán luôn sắp theo ngày bán, mới nhất trước.");
    }
}

internal static class SaleRuleExtensions
{
    // BR-SAL-01: có ít nhất một trong hai khách hàng, kênh bán. Lỗi gắn vào ô kênh bán vì khách hàng là ô chọn tùy chọn.
    public static void CustomerOrChannelRules<T>(
        this IRuleBuilderInitial<T, SalesChannel?> rule,
        Func<T, Guid?> customerId) =>
        rule.Cascade(CascadeMode.Stop)
            .Must((request, channel) => channel is not null || customerId(request) is not null)
            .WithMessage("Vui lòng chọn khách hàng hoặc kênh bán.")
            .IsInEnum().WithMessage("Kênh bán không hợp lệ.");

    public static void QualityGradeRules<T>(this IRuleBuilderInitial<T, QualityGrade?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại chất lượng.")
            .IsInEnum().WithMessage("Loại chất lượng không hợp lệ.");

    // BR-SAL-01: quantity > 0. Cột numeric(18,3).
    public static void SaleQuantityRules<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Số lượng tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");

    // BR-SAL-01: unit_price >= 0 (cho, tặng vẫn ghi được với giá 0). Cột numeric(18,4).
    public static void UnitPriceRules<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm.")
            .PrecisionScale(18, 4, ignoreTrailingZeros: true)
            .WithMessage("Đơn giá tối đa 4 chữ số sau dấu phẩy và 14 chữ số phần nguyên.");

    // BR-GEN-05: ngày đã xảy ra không được sau hôm nay (giờ Việt Nam).
    public static void SaleDateRules<T>(this IRuleBuilderInitial<T, DateOnly?> rule, IAppClock clock) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn ngày bán.")
            .Must(date => date <= clock.Today).WithMessage("Ngày bán không được sau hôm nay.");
}
