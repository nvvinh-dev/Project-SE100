using FluentValidation;

namespace HouseholdFarm.Api.Tests.Common.Probe;

public sealed class CreateProbeRequestValidator : AbstractValidator<CreateProbeRequest>
{
    public CreateProbeRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().WithMessage("Vui lòng nhập tên.");
        RuleFor(r => r.AreaM2).GreaterThan(0).WithMessage("Diện tích phải lớn hơn 0.");
        RuleFor(r => r.SoilType).IsInEnum().WithMessage("Vui lòng chọn loại đất.");
        RuleForEach(r => r.Lines).ChildRules(line =>
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("Số lượng phải lớn hơn 0."));

        // Cố ý không có WithMessage, để kiểm tra thông báo mặc định là tiếng Việt.
        RuleFor(r => r.Note).MaximumLength(10);
    }
}
