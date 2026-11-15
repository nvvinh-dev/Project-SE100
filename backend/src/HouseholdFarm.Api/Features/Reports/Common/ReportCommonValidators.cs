using FluentValidation;

namespace HouseholdFarm.Api.Features.Reports.Common;

public sealed class ReportFilterValidator : AbstractValidator<ReportFilter>
{
    public ReportFilterValidator()
    {
        RuleFor(f => f.From)
            .Must((filter, from) => !from.HasValue || !filter.To.HasValue || from.Value <= filter.To.Value)
            .WithMessage("Ngày bắt đầu phải nhỏ hơn hoặc bằng ngày kết thúc.");
    }
}

public sealed class ExportReportRequestValidator : AbstractValidator<ExportReportRequest>
{
    public ExportReportRequestValidator()
    {
        // ValidationFilter tự chạy validator của lớp cha ReportFilter nên không Include lại ở đây.
        RuleFor(r => r.Format)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn định dạng xuất báo cáo.")
            .IsInEnum().WithMessage("Định dạng xuất báo cáo không hợp lệ.");
    }
}
