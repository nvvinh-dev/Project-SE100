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

/// <summary>
/// Kiểm tra tham số truy vấn danh sách mùa vụ (FR-SEA-11).
/// </summary>
public sealed class SeasonQueryValidator : AbstractValidator<SeasonQuery>
{
    public SeasonQueryValidator()
    {
        RuleFor(q => q.Sort)
            .Must(sort => sort is null || SeasonQuery.SortFields.Contains(sort.TrimStart('-')))
            .WithMessage($"Chỉ sắp xếp được theo: {string.Join(", ", SeasonQuery.SortFields)}.");

        RuleFor(q => q.Status)
            .IsInEnum().WithMessage("Trạng thái mùa vụ không hợp lệ.");

        When(q => q.From.HasValue && q.To.HasValue, () =>
        {
            RuleFor(q => q.To)
                .GreaterThanOrEqualTo(q => q.From)
                .WithMessage("Ngày kết thúc lọc phải lớn hơn hoặc bằng ngày bắt đầu lọc.");
        });
    }
}

/// <summary>
/// Kiểm tra tham số truy vấn ô chọn mùa vụ (FR-SEA-11).
/// </summary>
public sealed class SeasonOptionQueryValidator : AbstractValidator<SeasonOptionQuery>
{
    public SeasonOptionQueryValidator()
    {
        RuleFor(q => q.Operation)
            .IsInEnum().When(q => q.Operation.HasValue)
            .WithMessage("Loại thao tác không hợp lệ.");

        RuleFor(q => q.Permission)
            .IsInEnum().When(q => q.Permission.HasValue)
            .WithMessage("Quyền mở rộng không hợp lệ.");
    }
}

