using FluentValidation;

namespace HouseholdFarm.Api.Features.Reports.Supplies;

public sealed class SupplyReportQueryValidator : AbstractValidator<SupplyReportQuery>
{
    public SupplyReportQueryValidator()
    {
        RuleFor(q => q.From)
            .Must((query, from) => !from.HasValue || !query.To.HasValue || from.Value <= query.To.Value)
            .WithMessage("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");

        RuleFor(q => q.Group)
            .IsInEnum()
            .WithMessage("Nhóm vật tư không hợp lệ.");
    }
}

public sealed class ExportSupplyReportRequestValidator : AbstractValidator<ExportSupplyReportRequest>
{
    public ExportSupplyReportRequestValidator()
    {
        RuleFor(r => r.From)
            .Must((request, from) => !from.HasValue || !request.To.HasValue || from.Value <= request.To.Value)
            .WithMessage("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");

        RuleFor(r => r.Group)
            .IsInEnum()
            .WithMessage("Nhóm vật tư không hợp lệ.");

        RuleFor(r => r.Format)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn định dạng xuất báo cáo.")
            .IsInEnum().WithMessage("Định dạng xuất báo cáo không hợp lệ.");
    }
}
