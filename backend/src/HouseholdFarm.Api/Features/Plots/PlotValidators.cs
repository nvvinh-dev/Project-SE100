using FluentValidation;

namespace HouseholdFarm.Api.Features.Plots;

// Validator chỉ kiểm tra định dạng, không truy vấn database. Trùng mã, mùa vụ đang dùng thửa: kiểm tra trong service.
public sealed class CreatePlotRequestValidator : AbstractValidator<CreatePlotRequest>
{
    public CreatePlotRequestValidator()
    {
        // Mã để trống được: service tự sinh (BR-CAT-01).
        RuleFor(r => r.Code).MaximumLength(20).WithMessage("Mã thửa tối đa 20 ký tự.");
        RuleFor(r => r.AreaM2)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Diện tích phải lớn hơn 0.")
            // Cột numeric(18,2): giá trị lẻ hơn 2 chữ số bị làm tròn (0.001 thành 0), quá lớn thì tràn cột.
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Diện tích tối đa 2 chữ số sau dấu phẩy và 16 chữ số phần nguyên.");
        RuleFor(r => r.SoilType)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại đất.")
            .IsInEnum().WithMessage("Loại đất không hợp lệ.");
        RuleFor(r => r.SoilTypeOther).MaximumLength(200).WithMessage("Ghi chú loại đất tối đa 200 ký tự.");
        RuleFor(r => r.WaterSource)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn nguồn nước.")
            .IsInEnum().WithMessage("Nguồn nước không hợp lệ.");
        RuleFor(r => r.WaterSourceOther).MaximumLength(200).WithMessage("Ghi chú nguồn nước tối đa 200 ký tự.");
    }
}

public sealed class UpdatePlotRequestValidator : AbstractValidator<UpdatePlotRequest>
{
    public UpdatePlotRequestValidator()
    {
        RuleFor(r => r.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập mã thửa.")
            .MaximumLength(20).WithMessage("Mã thửa tối đa 20 ký tự.");
        RuleFor(r => r.AreaM2)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithMessage("Diện tích phải lớn hơn 0.")
            // Cột numeric(18,2): giá trị lẻ hơn 2 chữ số bị làm tròn (0.001 thành 0), quá lớn thì tràn cột.
            .PrecisionScale(18, 2, ignoreTrailingZeros: true)
            .WithMessage("Diện tích tối đa 2 chữ số sau dấu phẩy và 16 chữ số phần nguyên.");
        RuleFor(r => r.SoilType)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại đất.")
            .IsInEnum().WithMessage("Loại đất không hợp lệ.");
        RuleFor(r => r.SoilTypeOther).MaximumLength(200).WithMessage("Ghi chú loại đất tối đa 200 ký tự.");
        RuleFor(r => r.WaterSource)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn nguồn nước.")
            .IsInEnum().WithMessage("Nguồn nước không hợp lệ.");
        RuleFor(r => r.WaterSourceOther).MaximumLength(200).WithMessage("Ghi chú nguồn nước tối đa 200 ký tự.");
        RuleFor(r => r.Status)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn tình trạng sử dụng.")
            .IsInEnum().WithMessage("Tình trạng sử dụng không hợp lệ.");
    }
}

public sealed class PlotQueryValidator : AbstractValidator<PlotQuery>
{
    public PlotQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra, không kiểm tra lại ở đây.
        RuleFor(q => q.Sort)
            .Must(sort => sort is null || PlotQuery.SortFields.Contains(sort.TrimStart('-')))
            .WithMessage($"Chỉ sắp xếp được theo: {string.Join(", ", PlotQuery.SortFields)}.");
        RuleFor(q => q.Status).IsInEnum().WithMessage("Tình trạng sử dụng không hợp lệ.");
        RuleFor(q => q.SoilType).IsInEnum().WithMessage("Loại đất không hợp lệ.");
        RuleFor(q => q.WaterSource).IsInEnum().WithMessage("Nguồn nước không hợp lệ.");
    }
}
