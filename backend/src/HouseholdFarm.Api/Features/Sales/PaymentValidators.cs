using FluentValidation;
using HouseholdFarm.Api.Common.Time;

namespace HouseholdFarm.Api.Features.Sales;

// Validator chỉ kiểm tra định dạng. Công nợ còn lại, giao dịch đã hủy, trạng thái mùa vụ: kiểm tra trong service.
public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator(IAppClock clock)
    {
        RuleFor(r => r.RequestId).NotEmpty().WithMessage("Thiếu mã yêu cầu.");
        RuleFor(r => r.SaleId).NotNull().WithMessage("Thiếu giao dịch bán.");
        // BR-SAL-06: amount > 0. Cột numeric(18,2).
        RuleFor(r => r.Amount)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Số tiền phải lớn hơn 0.")
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Số tiền tối đa 2 chữ số sau dấu phẩy và 16 chữ số phần nguyên.");
        // BR-SAL-06, BR-GEN-05: được trước ngày bán (tiền cọc), chỉ không được sau hôm nay.
        RuleFor(r => r.PaymentDate)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn ngày nhận tiền.")
            .Must(date => date <= clock.Today).WithMessage("Ngày nhận tiền không được sau hôm nay.");
    }
}

public sealed class VoidPaymentRequestValidator : AbstractValidator<VoidPaymentRequest>
{
    public VoidPaymentRequestValidator()
    {
        // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
        RuleFor(r => r.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do hủy.");
    }
}
