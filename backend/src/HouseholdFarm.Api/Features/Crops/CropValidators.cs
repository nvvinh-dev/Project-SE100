using FluentValidation;

namespace HouseholdFarm.Api.Features.Crops;

// Validator chỉ kiểm tra định dạng, không truy vấn database. Trùng tên, khóa đơn vị, mùa vụ đang dùng cây: kiểm tra trong service.
public sealed class CreateCropRequestValidator : AbstractValidator<CreateCropRequest>
{
    public CreateCropRequestValidator()
    {
        RuleFor(r => r.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập tên cây trồng.")
            .MaximumLength(200).WithMessage("Tên cây trồng tối đa 200 ký tự.");
        RuleFor(r => r.Group)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn nhóm cây.")
            .IsInEnum().WithMessage("Nhóm cây không hợp lệ.");
        RuleFor(r => r.GroupOther).MaximumLength(200).WithMessage("Ghi chú nhóm cây tối đa 200 ký tự.");
        RuleFor(r => r.YieldUnit)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn đơn vị sản lượng.")
            .IsInEnum().WithMessage("Đơn vị sản lượng không hợp lệ.");
    }
}

public sealed class UpdateCropRequestValidator : AbstractValidator<UpdateCropRequest>
{
    public UpdateCropRequestValidator()
    {
        RuleFor(r => r.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập tên cây trồng.")
            .MaximumLength(200).WithMessage("Tên cây trồng tối đa 200 ký tự.");
        RuleFor(r => r.Group)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn nhóm cây.")
            .IsInEnum().WithMessage("Nhóm cây không hợp lệ.");
        RuleFor(r => r.GroupOther).MaximumLength(200).WithMessage("Ghi chú nhóm cây tối đa 200 ký tự.");
        RuleFor(r => r.YieldUnit)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn đơn vị sản lượng.")
            .IsInEnum().WithMessage("Đơn vị sản lượng không hợp lệ.");
        RuleFor(r => r.Status)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn trạng thái.")
            .IsInEnum().WithMessage("Trạng thái không hợp lệ.");
    }
}

public sealed class CropQueryValidator : AbstractValidator<CropQuery>
{
    public CropQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra, không kiểm tra lại ở đây.
        RuleFor(q => q.Sort)
            .Must(sort => sort is null || CropQuery.SortFields.Contains(sort.TrimStart('-')))
            .WithMessage($"Chỉ sắp xếp được theo: {string.Join(", ", CropQuery.SortFields)}.");
        RuleFor(q => q.Group).IsInEnum().WithMessage("Nhóm cây không hợp lệ.");
        RuleFor(q => q.Status).IsInEnum().WithMessage("Trạng thái không hợp lệ.");
    }
}
