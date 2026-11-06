using FluentValidation;

namespace HouseholdFarm.Api.Features.Seasons;

/// <summary>
/// Kiểm tra định dạng yêu cầu tạo mùa vụ (FR-SEA-01, BR-SEA-01, BR-GEN-05).
/// </summary>
public sealed class CreateSeasonRequestValidator : AbstractValidator<CreateSeasonRequest>
{
    public CreateSeasonRequestValidator()
    {
        RuleFor(r => r.PlotId)
            .NotEmpty().WithMessage("Vui lòng chọn thửa đất.");

        RuleFor(r => r.CropId)
            .NotEmpty().WithMessage("Vui lòng chọn cây trồng.");

        RuleFor(r => r.StartDate)
            .NotEmpty().WithMessage("Vui lòng nhập ngày bắt đầu.");

        RuleFor(r => r.ExpectedHarvestDate)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập ngày dự kiến thu hoạch.")
            // BR-SEA-01: expected_harvest_date >= start_date
            .GreaterThanOrEqualTo(r => r.StartDate)
            .WithMessage("Ngày dự kiến thu hoạch phải lớn hơn hoặc bằng ngày bắt đầu.");

        RuleFor(r => r.TargetYield)
            .Cascade(CascadeMode.Stop)
            // BR-SEA-01: target_yield > 0
            .GreaterThan(0).WithMessage("Mục tiêu sản lượng phải lớn hơn 0.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Mục tiêu sản lượng tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");
    }
}

/// <summary>
/// Kiểm tra định dạng yêu cầu sửa mùa vụ (FR-SEA-07, BR-SEA-01, BR-GEN-05, BR-GEN-11).
/// </summary>
public sealed class UpdateSeasonRequestValidator : AbstractValidator<UpdateSeasonRequest>
{
    public UpdateSeasonRequestValidator()
    {
        RuleFor(r => r.PlotId)
            .NotEmpty().WithMessage("Vui lòng chọn thửa đất.");

        RuleFor(r => r.CropId)
            .NotEmpty().WithMessage("Vui lòng chọn cây trồng.");

        RuleFor(r => r.StartDate)
            .NotEmpty().WithMessage("Vui lòng nhập ngày bắt đầu.");

        RuleFor(r => r.ExpectedHarvestDate)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập ngày dự kiến thu hoạch.")
            // BR-SEA-01: expected_harvest_date >= start_date
            .GreaterThanOrEqualTo(r => r.StartDate)
            .WithMessage("Ngày dự kiến thu hoạch phải lớn hơn hoặc bằng ngày bắt đầu.");

        RuleFor(r => r.TargetYield)
            .Cascade(CascadeMode.Stop)
            // BR-SEA-01: target_yield > 0
            .GreaterThan(0).WithMessage("Mục tiêu sản lượng phải lớn hơn 0.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Mục tiêu sản lượng tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");

        RuleFor(r => r.Version)
            .NotEmpty().WithMessage("Thiếu thông tin phiên bản dữ liệu.");
    }
}

