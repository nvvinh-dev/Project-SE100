using FluentValidation;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Harvests;

// Validator chỉ kiểm tra định dạng, không truy vấn database. Ngày bắt đầu và trạng thái mùa vụ kiểm tra trong service.
public sealed class CreateHarvestRecordRequestValidator : AbstractValidator<CreateHarvestRecordRequest>
{
    public CreateHarvestRecordRequestValidator(IAppClock clock)
    {
        RuleFor(r => r.RequestId).NotEmpty().WithMessage("Thiếu mã yêu cầu.");
        RuleFor(r => r.SeasonId).NotEmpty().WithMessage("Vui lòng chọn mùa vụ.");
        RuleFor(r => r.HarvestDate).HarvestDateRules(clock);
        RuleFor(r => r.QualityGrade).QualityGradeRules();
        RuleFor(r => r.Quantity).HarvestQuantityRules();
        RuleFor(r => r.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public sealed class CorrectHarvestRecordRequestValidator : AbstractValidator<CorrectHarvestRecordRequest>
{
    public CorrectHarvestRecordRequestValidator(IAppClock clock)
    {
        RuleFor(r => r.HarvestDate).HarvestDateRules(clock);
        RuleFor(r => r.QualityGrade).QualityGradeRules();
        RuleFor(r => r.Quantity).HarvestQuantityRules();
        RuleFor(r => r.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
        // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
        RuleFor(r => r.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do điều chỉnh.")
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
        RuleFor(r => r.Version).NotEmpty().WithMessage("Thiếu thông tin phiên bản dữ liệu.");
    }
}

public sealed class VoidHarvestRecordRequestValidator : AbstractValidator<VoidHarvestRecordRequest>
{
    public VoidHarvestRecordRequestValidator()
    {
        // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
        RuleFor(r => r.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do hủy.")
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");
    }
}

public sealed class HarvestRecordQueryValidator : AbstractValidator<HarvestRecordQuery>
{
    public HarvestRecordQueryValidator()
    {
        RuleFor(q => q.QualityGrade).IsInEnum().WithMessage("Loại chất lượng không hợp lệ.");
        RuleFor(q => q.To)
            .Must((query, to) => query.From is null || to is null || query.From <= to)
            .WithMessage("Ngày kết thúc không được trước ngày bắt đầu.");
        When(q => q.ForSaleLink == true, () =>
        {
            RuleFor(q => q.SeasonId)
                .NotNull().WithMessage("Chế độ liên kết giao dịch bán yêu cầu chọn mùa vụ.");
            RuleFor(q => q.QualityGrade)
                .NotNull().WithMessage("Chế độ liên kết giao dịch bán yêu cầu chọn loại chất lượng.");
        });
    }
}

internal static class HarvestRuleExtensions
{
    // BR-GEN-05: ngày thu hoạch không được sau hôm nay (giờ Việt Nam).
    public static void HarvestDateRules<T>(this IRuleBuilderInitial<T, DateOnly?> rule, IAppClock clock) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn ngày thu hoạch.")
            .Must(date => date <= clock.Today).WithMessage("Ngày thu hoạch không được sau hôm nay.");

    public static void QualityGradeRules<T>(this IRuleBuilderInitial<T, QualityGrade?> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại chất lượng.")
            .IsInEnum().WithMessage("Loại chất lượng không hợp lệ.");

    // BR-HAR-01: quantity > 0, cột numeric(18,3).
    public static void HarvestQuantityRules<T>(this IRuleBuilderInitial<T, decimal> rule) =>
        rule.Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Sản lượng phải lớn hơn 0.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Sản lượng tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");
}
