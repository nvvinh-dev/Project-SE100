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
            .IsInEnum().WithMessage("Loại giao dịch không hợp lệ.");
        RuleFor(r => r.SupplyId).NotEmpty().WithMessage("Vui lòng chọn vật tư.");
        RuleFor(r => r.RequestId).NotEmpty().WithMessage("Thiếu mã yêu cầu.");
        RuleFor(r => r.TransactionDate)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn ngày giao dịch.")
            // BR-GEN-05: "hôm nay" theo giờ Việt Nam.
            .Must(date => date <= clock.Today).WithMessage("Ngày giao dịch không được sau hôm nay.");

        // Cột numeric(18,3): giá trị lẻ hơn 3 chữ số bị làm tròn, quá lớn thì tràn cột.
        RuleFor(r => r.Quantity)
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Số lượng tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");

        When(r => r.Type == StockTransactionType.In, () =>
        {
            RuleFor(r => r.Quantity).GreaterThan(0).WithMessage("Số lượng nhập phải lớn hơn 0.");
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

        When(r => r.Type == StockTransactionType.Out, () =>
        {
            // Gửi lên số dương, service lưu thành số âm.
            RuleFor(r => r.Quantity).GreaterThan(0).WithMessage("Số lượng xuất phải lớn hơn 0.");
            // BR-SUP-06: xuất kho bắt buộc gắn mùa vụ.
            RuleFor(r => r.SeasonId).NotNull().WithMessage("Vui lòng chọn mùa vụ.");
        });

        When(r => r.Type == StockTransactionType.Adjustment, () =>
        {
            // BR-SUP-09: có dấu, tăng dương, giảm âm.
            RuleFor(r => r.Quantity).NotEqual(0).WithMessage("Số lượng điều chỉnh phải khác 0.");
            // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
            RuleFor(r => r.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do điều chỉnh.");
            RuleFor(r => r.SeasonId).Null().WithMessage("Điều chỉnh tồn không gắn với mùa vụ.");
        });
    }
}

public sealed class VoidStockTransactionRequestValidator : AbstractValidator<VoidStockTransactionRequest>
{
    public VoidStockTransactionRequestValidator()
    {
        // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
        RuleFor(r => r.Reason).NotEmpty().WithMessage("Vui lòng nhập lý do hủy.");
    }
}

public sealed class StockTransactionQueryValidator : AbstractValidator<StockTransactionQuery>
{
    public StockTransactionQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra, không kiểm tra lại ở đây.
        RuleFor(q => q.Type).IsInEnum().WithMessage("Loại giao dịch không hợp lệ.");
        RuleFor(q => q.To)
            .Must((query, to) => query.From is null || to is null || query.From <= to)
            .WithMessage("Ngày kết thúc không được trước ngày bắt đầu.");
    }
}
