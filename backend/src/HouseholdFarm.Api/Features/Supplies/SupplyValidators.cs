using FluentValidation;

namespace HouseholdFarm.Api.Features.Supplies;

// Validator chỉ kiểm tra định dạng, không truy vấn database. Trùng mã, khóa đơn vị: kiểm tra trong service.
public sealed class CreateSupplyRequestValidator : AbstractValidator<CreateSupplyRequest>
{
    public CreateSupplyRequestValidator()
    {
        // Mã để trống được: service tự sinh (BR-CAT-01).
        RuleFor(r => r.Code).MaximumLength(20).WithMessage("Mã vật tư tối đa 20 ký tự.");
        RuleFor(r => r.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập tên vật tư.")
            .MaximumLength(200).WithMessage("Tên vật tư tối đa 200 ký tự.");
        RuleFor(r => r.Group)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn nhóm vật tư.")
            .IsInEnum().WithMessage("Nhóm vật tư không hợp lệ.");
        RuleFor(r => r.Unit)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn đơn vị tính.")
            .IsInEnum().WithMessage("Đơn vị tính không hợp lệ.");
        RuleFor(r => r.MinStockLevel)
            .Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Ngưỡng tồn tối thiểu không được âm.")
            // Cột numeric(18,3): giá trị lẻ hơn 3 chữ số bị làm tròn, quá lớn thì tràn cột.
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Ngưỡng tồn tối thiểu tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");
        RuleFor(r => r.ReferencePrice)
            .Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Giá nhập tham khảo không được âm.")
            .PrecisionScale(18, 4, ignoreTrailingZeros: true)
            .WithMessage("Giá nhập tham khảo tối đa 4 chữ số sau dấu phẩy và 14 chữ số phần nguyên.");
    }
}

public sealed class UpdateSupplyRequestValidator : AbstractValidator<UpdateSupplyRequest>
{
    public UpdateSupplyRequestValidator()
    {
        RuleFor(r => r.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập mã vật tư.")
            .MaximumLength(20).WithMessage("Mã vật tư tối đa 20 ký tự.");
        RuleFor(r => r.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập tên vật tư.")
            .MaximumLength(200).WithMessage("Tên vật tư tối đa 200 ký tự.");
        RuleFor(r => r.Group)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn nhóm vật tư.")
            .IsInEnum().WithMessage("Nhóm vật tư không hợp lệ.");
        RuleFor(r => r.Unit)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn đơn vị tính.")
            .IsInEnum().WithMessage("Đơn vị tính không hợp lệ.");
        RuleFor(r => r.MinStockLevel)
            .Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Ngưỡng tồn tối thiểu không được âm.")
            // Cột numeric(18,3): giá trị lẻ hơn 3 chữ số bị làm tròn, quá lớn thì tràn cột.
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Ngưỡng tồn tối thiểu tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");
        RuleFor(r => r.ReferencePrice)
            .Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(0).WithMessage("Giá nhập tham khảo không được âm.")
            .PrecisionScale(18, 4, ignoreTrailingZeros: true)
            .WithMessage("Giá nhập tham khảo tối đa 4 chữ số sau dấu phẩy và 14 chữ số phần nguyên.");
        RuleFor(r => r.Status)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn trạng thái.")
            .IsInEnum().WithMessage("Trạng thái không hợp lệ.");
    }
}

public sealed class SupplyQueryValidator : AbstractValidator<SupplyQuery>
{
    public SupplyQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra, không kiểm tra lại ở đây.
        RuleFor(q => q.Sort)
            .Must(sort => sort is null || SupplyQuery.SortFields.Contains(sort.TrimStart('-')))
            .WithMessage($"Chỉ sắp xếp được theo: {string.Join(", ", SupplyQuery.SortFields)}.");
        RuleFor(q => q.Group).IsInEnum().WithMessage("Nhóm vật tư không hợp lệ.");
        RuleFor(q => q.Status).IsInEnum().WithMessage("Trạng thái không hợp lệ.");
    }
}
