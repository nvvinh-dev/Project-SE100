using FluentValidation;

namespace HouseholdFarm.Api.Features.Reports.FarmTasks;

public sealed class FarmTaskReportQueryValidator : AbstractValidator<FarmTaskReportQuery>
{
    public FarmTaskReportQueryValidator()
    {
        RuleFor(q => q.GroupBy)
            .IsInEnum()
            .WithMessage("Kiểu nhóm báo cáo công việc không hợp lệ.");
    }
}

public sealed class ExportFarmTaskReportRequestValidator : AbstractValidator<ExportFarmTaskReportRequest>
{
    public ExportFarmTaskReportRequestValidator()
    {
        RuleFor(q => q.GroupBy)
            .IsInEnum()
            .WithMessage("Kiểu nhóm báo cáo công việc không hợp lệ.");
    }
}
