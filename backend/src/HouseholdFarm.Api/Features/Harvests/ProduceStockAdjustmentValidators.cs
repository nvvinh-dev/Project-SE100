using FluentValidation;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Harvests;

public sealed class ProduceStockOverviewQueryValidator : AbstractValidator<ProduceStockOverviewQuery>
{
    public ProduceStockOverviewQueryValidator()
    {
        RuleFor(q => q.QualityGrade).IsInEnum().WithMessage("Loại chất lượng không hợp lệ.");
    }
}

public sealed class ProduceStockAdjustmentQueryValidator : AbstractValidator<ProduceStockAdjustmentQuery>
{
    public ProduceStockAdjustmentQueryValidator()
    {
        RuleFor(q => q.QualityGrade).IsInEnum().WithMessage("Loại chất lượng không hợp lệ.");
    }
}

public sealed class CreateProduceStockAdjustmentRequestValidator : AbstractValidator<CreateProduceStockAdjustmentRequest>
{
    public CreateProduceStockAdjustmentRequestValidator(IAppClock clock)
    {
        RuleFor(r => r.RequestId).NotEmpty().WithMessage("Thiếu mã yêu cầu.");
        RuleFor(r => r.SeasonId).NotEmpty().WithMessage("Vui lòng chọn mùa vụ.");
        RuleFor(r => r.QualityGrade)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại chất lượng.")
            .IsInEnum().WithMessage("Loại chất lượng không hợp lệ.");

        // BR-HAR-05: lượng giảm > 0, cột numeric(18,3).
        RuleFor(r => r.Quantity)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Lượng điều chỉnh phải lớn hơn 0.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Lượng điều chỉnh tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");

        // BR-GEN-05: ngày điều chỉnh không được sau hôm nay (giờ Việt Nam).
        RuleFor(r => r.AdjustmentDate)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn ngày điều chỉnh.")
            .Must(date => date <= clock.Today).WithMessage("Ngày điều chỉnh không được sau hôm nay.");

        RuleFor(r => r.Reason)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn lý do điều chỉnh.")
            .IsInEnum().WithMessage("Lý do điều chỉnh không hợp lệ.");

        RuleFor(r => r.ReasonOther)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập lý do cụ thể khi chọn Khác.")
            .MaximumLength(200).WithMessage("Lý do khác tối đa 200 ký tự.")
            .When(r => r.Reason == ProduceAdjustmentReason.Other);

        RuleFor(r => r.ReasonOther)
            .MaximumLength(200).WithMessage("Lý do khác tối đa 200 ký tự.")
            .When(r => r.Reason != ProduceAdjustmentReason.Other && r.ReasonOther is not null);

        RuleFor(r => r.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public sealed class VoidProduceStockAdjustmentRequestValidator : AbstractValidator<VoidProduceStockAdjustmentRequest>
{
    public VoidProduceStockAdjustmentRequestValidator()
    {
        // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
        RuleFor(r => r.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do hủy.")
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
    }
}
