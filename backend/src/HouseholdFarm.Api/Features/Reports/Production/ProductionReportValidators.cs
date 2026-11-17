using FluentValidation;

namespace HouseholdFarm.Api.Features.Reports.Production;

public sealed class ProductionReportQueryValidator : AbstractValidator<ProductionReportQuery>
{
    public ProductionReportQueryValidator()
    {
        RuleFor(q => q.DeviationThreshold)
            .GreaterThan(0m)
            .When(q => q.DeviationThreshold.HasValue)
            .WithMessage("Ngưỡng lệch sản lượng phải là số dương.");

        RuleFor(q => q.CompareBy)
            .IsInEnum()
            .WithMessage("Tiêu chí so sánh mùa trước không hợp lệ.");
    }
}

public sealed class ExportProductionReportRequestValidator : AbstractValidator<ExportProductionReportRequest>
{
    public ExportProductionReportRequestValidator()
    {
        RuleFor(q => q.DeviationThreshold)
            .GreaterThan(0m)
            .When(q => q.DeviationThreshold.HasValue)
            .WithMessage("Ngưỡng lệch sản lượng phải là số dương.");

        RuleFor(q => q.CompareBy)
            .IsInEnum()
            .WithMessage("Tiêu chí so sánh mùa trước không hợp lệ.");
    }
}
