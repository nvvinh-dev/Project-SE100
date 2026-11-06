using FluentValidation;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Supplies;

// Validator chỉ kiểm tra định dạng theo từng loại giao dịch; tồn, trạng thái mùa vụ, trạng thái vật tư kiểm tra trong service.
public sealed class CreateStockTransactionRequestValidator : AbstractValidator<CreateStockTransactionRequest>
{
    // Cột total_cost numeric(18,2): thành tiền phải còn vừa 16 chữ số phần nguyên.
    private const double MaxTotalCost = 1e16;

    public CreateStockTransactionRequestValidator(IAppClock clock)
    {
        RuleFor(r => r.Type)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại giao dịch.")
            .IsInEnum().WithMessage("Loại giao dịch không hợp lệ.")
            .Equal(StockTransactionType.In).WithMessage("Loại giao dịch này chưa được hỗ trợ.");
        RuleFor(r => r.SupplyId).NotEmpty().WithMessage("Vui lòng chọn vật tư.");
        RuleFor(r => r.RequestId).NotEmpty().WithMessage("Thiếu mã yêu cầu.");
        RuleFor(r => r.TransactionDate)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn ngày giao dịch.")
            // BR-GEN-05: "hôm nay" theo giờ Việt Nam.
            .Must(date => date <= clock.Today).WithMessage("Ngày giao dịch không được sau hôm nay.");

        When(r => r.Type == StockTransactionType.In, () =>
        {
            RuleFor(r => r.Quantity)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithMessage("Số lượng nhập phải lớn hơn 0.")
                // Cột numeric(18,3): giá trị lẻ hơn 3 chữ số bị làm tròn, quá lớn thì tràn cột.
                .PrecisionScale(18, 3, ignoreTrailingZeros: true)
                .WithMessage("Số lượng tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");
            RuleFor(r => r.UnitCost)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Vui lòng nhập đơn giá nhập.")
                .GreaterThanOrEqualTo(0).WithMessage("Đơn giá nhập không được âm.")
                .PrecisionScale(18, 4, ignoreTrailingZeros: true)
                .WithMessage("Đơn giá tối đa 4 chữ số sau dấu phẩy và 14 chữ số phần nguyên.");
            RuleFor(r => r)
                .Must(r => (double)r.Quantity * (double)(r.UnitCost ?? 0) < MaxTotalCost)
                .OverridePropertyName(nameof(CreateStockTransactionRequest.UnitCost))
                .WithMessage("Thành tiền vượt quá mức cho phép.");
            // BR-SUP-05: nhập kho không tạo chi phí mùa vụ.
            RuleFor(r => r.SeasonId).Null().WithMessage("Nhập kho không gắn với mùa vụ.");
        });
    }
}
