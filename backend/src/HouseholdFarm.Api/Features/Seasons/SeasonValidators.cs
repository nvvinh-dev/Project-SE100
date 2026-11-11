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
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại thao tác.")
            .IsInEnum().WithMessage("Loại thao tác không hợp lệ.");
    }
}

/// <summary>
/// Kiểm tra định dạng yêu cầu phân công người vào mùa vụ (FR-SEA-06).
/// </summary>
public sealed class CreateSeasonAssignmentRequestValidator : AbstractValidator<CreateSeasonAssignmentRequest>
{
    public CreateSeasonAssignmentRequestValidator()
    {
        RuleFor(r => r.SeasonId)
            .NotEmpty().WithMessage("Vui lòng chọn mùa vụ.");

        RuleFor(r => r.UserId)
            .NotEmpty().WithMessage("Vui lòng chọn người được phân công.");
    }
}

/// <summary>
/// Kiểm tra định dạng yêu cầu tạo ghi chú kinh nghiệm (FR-SEA-13, BR-SEA-11).
/// </summary>
public sealed class CreateSeasonNoteRequestValidator : AbstractValidator<CreateSeasonNoteRequest>
{
    public CreateSeasonNoteRequestValidator()
    {
        RuleFor(r => r.SeasonId)
            .NotEmpty().WithMessage("Vui lòng chọn mùa vụ.");

        RuleFor(r => r.Content)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập nội dung ghi chú.")
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Nội dung ghi chú không được chỉ chứa khoảng trắng.")
            .MaximumLength(2000).WithMessage("Nội dung ghi chú tối đa 2000 ký tự.");
    }
}

/// <summary>
/// Kiểm tra định dạng yêu cầu sửa ghi chú kinh nghiệm (FR-SEA-13, BR-SEA-11).
/// </summary>
public sealed class UpdateSeasonNoteRequestValidator : AbstractValidator<UpdateSeasonNoteRequest>
{
    public UpdateSeasonNoteRequestValidator()
    {
        RuleFor(r => r.Content)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập nội dung ghi chú.")
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Nội dung ghi chú không được chỉ chứa khoảng trắng.")
            .MaximumLength(2000).WithMessage("Nội dung ghi chú tối đa 2000 ký tự.");

        RuleFor(r => r.Version)
            .NotEmpty().WithMessage("Thiếu thông tin phiên bản dữ liệu.");
    }
}

/// <summary>
/// Kiểm tra định dạng yêu cầu khai báo vật tư dự kiến (FR-SEA-04).
/// </summary>
public sealed class CreateSeasonSupplyPlanRequestValidator : AbstractValidator<CreateSeasonSupplyPlanRequest>
{
    public CreateSeasonSupplyPlanRequestValidator()
    {
        RuleFor(r => r.SeasonId)
            .NotEmpty().WithMessage("Vui lòng chọn mùa vụ.");

        RuleFor(r => r.SupplyId)
            .NotEmpty().WithMessage("Vui lòng chọn vật tư.");

        RuleFor(r => r.PlannedQuantity)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Lượng dự kiến phải lớn hơn 0.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Lượng dự kiến tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");
    }
}

/// <summary>
/// Kiểm tra định dạng yêu cầu sửa lượng vật tư dự kiến (FR-SEA-04).
/// </summary>
public sealed class UpdateSeasonSupplyPlanRequestValidator : AbstractValidator<UpdateSeasonSupplyPlanRequest>
{
    public UpdateSeasonSupplyPlanRequestValidator()
    {
        RuleFor(r => r.PlannedQuantity)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Lượng dự kiến phải lớn hơn 0.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Lượng dự kiến tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");

        RuleFor(r => r.Version)
            .NotEmpty().WithMessage("Thiếu thông tin phiên bản dữ liệu.");
    }
}

/// <summary>
/// Kiểm tra định dạng yêu cầu hủy mùa vụ (FR-SEA-10, BR-GEN-06).
/// </summary>
public sealed class CancelSeasonRequestValidator : AbstractValidator<CancelSeasonRequest>
{
    public CancelSeasonRequestValidator()
    {
        RuleFor(r => r.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập lý do hủy mùa vụ.")
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Lý do hủy không được chỉ chứa khoảng trắng.")
            .MaximumLength(1000).WithMessage("Lý do hủy tối đa 1000 ký tự.");
    }
}

/// <summary>
/// Kiểm tra định dạng yêu cầu mở lại mùa vụ (FR-SEA-14, BR-GEN-06).
/// </summary>
public sealed class ReopenSeasonRequestValidator : AbstractValidator<ReopenSeasonRequest>
{
    public ReopenSeasonRequestValidator()
    {
        RuleFor(r => r.Reason)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập lý do mở lại mùa vụ.")
            .Must(r => !string.IsNullOrWhiteSpace(r)).WithMessage("Lý do mở lại không được chỉ chứa khoảng trắng.")
            .MaximumLength(1000).WithMessage("Lý do mở lại tối đa 1000 ký tự.");
    }
}

