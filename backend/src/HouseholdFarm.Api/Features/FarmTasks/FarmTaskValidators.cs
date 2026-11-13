using FluentValidation;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.FarmTasks;

public sealed class FarmTaskPlannedSupplyInputValidator : AbstractValidator<FarmTaskPlannedSupplyInput>
{
    public FarmTaskPlannedSupplyInputValidator()
    {
        RuleFor(x => x.SupplyId)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn vật tư.")
            .NotEqual(Guid.Empty).WithMessage("Vật tư không hợp lệ.");

        RuleFor(x => x.PlannedQuantity)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng nhập lượng dự kiến.")
            .GreaterThan(0).WithMessage("Lượng vật tư dự kiến phải lớn hơn 0.")
            .PrecisionScale(18, 3, ignoreTrailingZeros: true)
            .WithMessage("Lượng vật tư tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");
    }
}

public sealed class CreateFarmTaskRequestValidator : AbstractValidator<CreateFarmTaskRequest>
{
    public CreateFarmTaskRequestValidator()
    {
        RuleFor(r => r.SeasonId)
            .NotEmpty().WithMessage("Vui lòng chọn mùa vụ.");

        RuleFor(r => r.Type)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại công việc.")
            .IsInEnum().WithMessage("Loại công việc không hợp lệ.");

        RuleFor(r => r.TypeOther)
            .MaximumLength(200).WithMessage("Tên loại công việc tối đa 200 ký tự.");

        When(r => r.Type == FarmTaskType.Other, () =>
        {
            RuleFor(r => r.TypeOther)
                .NotEmpty().WithMessage("Vui lòng nhập tên loại công việc.");
        });

        RuleFor(r => r.ScheduledDate)
            .NotNull().WithMessage("Vui lòng chọn ngày dự kiến.");

        When(r => r.PlannedDurationMinutes.HasValue, () =>
        {
            RuleFor(r => r.PlannedDurationMinutes!.Value)
                .GreaterThan(0).WithMessage("Thời lượng dự kiến phải lớn hơn 0 phút.");
        });

        RuleFor(r => r.Note)
            .MaximumLength(2000).WithMessage("Ghi chú tối đa 2000 ký tự.");

        RuleForEach(r => r.Supplies!)
            .SetValidator(new FarmTaskPlannedSupplyInputValidator())
            .When(r => r.Supplies is not null);

        RuleFor(r => r.Supplies)
            .Must(supplies => supplies is null || supplies
                .Where(s => s.SupplyId.HasValue && s.SupplyId.Value != Guid.Empty)
                .Select(s => s.SupplyId!.Value)
                .Distinct()
                .Count() == supplies.Count(s => s.SupplyId.HasValue && s.SupplyId.Value != Guid.Empty))
            .WithMessage("Mỗi vật tư chỉ được khai báo một dòng trong công việc.");
    }
}

public sealed class CreateRecurringFarmTasksRequestValidator : AbstractValidator<CreateRecurringFarmTasksRequest>
{
    public CreateRecurringFarmTasksRequestValidator()
    {
        RuleFor(r => r.SeasonId)
            .NotEmpty().WithMessage("Vui lòng chọn mùa vụ.");

        RuleFor(r => r.Type)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại công việc.")
            .IsInEnum().WithMessage("Loại công việc không hợp lệ.");

        RuleFor(r => r.TypeOther)
            .MaximumLength(200).WithMessage("Tên loại công việc tối đa 200 ký tự.");

        When(r => r.Type == FarmTaskType.Other, () =>
        {
            RuleFor(r => r.TypeOther)
                .NotEmpty().WithMessage("Vui lòng nhập tên loại công việc.");
        });

        RuleFor(r => r.FirstScheduledDate)
            .NotNull().WithMessage("Vui lòng chọn ngày bắt đầu lặp.");

        RuleFor(r => r.IntervalDays)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng nhập chu kỳ lặp.")
            .GreaterThanOrEqualTo(1).WithMessage("Chu kỳ lặp phải tối thiểu 1 ngày.");

        When(r => r.PlannedDurationMinutes.HasValue, () =>
        {
            RuleFor(r => r.PlannedDurationMinutes!.Value)
                .GreaterThan(0).WithMessage("Thời lượng dự kiến phải lớn hơn 0 phút.");
        });

        RuleFor(r => r.Note)
            .MaximumLength(2000).WithMessage("Ghi chú tối đa 2000 ký tự.");

        RuleForEach(r => r.Supplies!)
            .SetValidator(new FarmTaskPlannedSupplyInputValidator())
            .When(r => r.Supplies is not null);

        RuleFor(r => r.Supplies)
            .Must(supplies => supplies is null || supplies
                .Where(s => s.SupplyId.HasValue && s.SupplyId.Value != Guid.Empty)
                .Select(s => s.SupplyId!.Value)
                .Distinct()
                .Count() == supplies.Count(s => s.SupplyId.HasValue && s.SupplyId.Value != Guid.Empty))
            .WithMessage("Mỗi vật tư chỉ được khai báo một dòng trong công việc.");
    }
}

public sealed class UpdateFarmTaskRequestValidator : AbstractValidator<UpdateFarmTaskRequest>
{
    public UpdateFarmTaskRequestValidator()
    {
        RuleFor(r => r.Type)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn loại công việc.")
            .IsInEnum().WithMessage("Loại công việc không hợp lệ.");

        RuleFor(r => r.TypeOther)
            .MaximumLength(200).WithMessage("Tên loại công việc tối đa 200 ký tự.");

        When(r => r.Type == FarmTaskType.Other, () =>
        {
            RuleFor(r => r.TypeOther)
                .NotEmpty().WithMessage("Vui lòng nhập tên loại công việc.");
        });

        RuleFor(r => r.ScheduledDate)
            .NotNull().WithMessage("Vui lòng chọn ngày dự kiến.");

        When(r => r.PlannedDurationMinutes.HasValue, () =>
        {
            RuleFor(r => r.PlannedDurationMinutes!.Value)
                .GreaterThan(0).WithMessage("Thời lượng dự kiến phải lớn hơn 0 phút.");
        });

        RuleFor(r => r.Note)
            .MaximumLength(2000).WithMessage("Ghi chú tối đa 2000 ký tự.");

        RuleForEach(r => r.Supplies!)
            .SetValidator(new FarmTaskPlannedSupplyInputValidator())
            .When(r => r.Supplies is not null);

        RuleFor(r => r.Supplies)
            .Must(supplies => supplies is null || supplies
                .Where(s => s.SupplyId.HasValue && s.SupplyId.Value != Guid.Empty)
                .Select(s => s.SupplyId!.Value)
                .Distinct()
                .Count() == supplies.Count(s => s.SupplyId.HasValue && s.SupplyId.Value != Guid.Empty))
            .WithMessage("Mỗi vật tư chỉ được khai báo một dòng trong công việc.");
    }
}

public sealed class FarmTaskQueryValidator : AbstractValidator<FarmTaskQuery>
{
    public FarmTaskQueryValidator()
    {
        RuleFor(q => q.Sort)
            .Must(sort => sort is null || FarmTaskQuery.SortFields.Contains(sort.TrimStart('-')))
            .WithMessage($"Chỉ sắp xếp được theo: {string.Join(", ", FarmTaskQuery.SortFields)}.");

        RuleFor(q => q.Status)
            .IsInEnum().WithMessage("Trạng thái công việc không hợp lệ.");

        When(q => q.From.HasValue && q.To.HasValue, () =>
        {
            RuleFor(q => q.To!.Value)
                .GreaterThanOrEqualTo(q => q.From!.Value)
                .WithMessage("Ngày kết thúc khoảng lọc phải lớn hơn hoặc bằng ngày bắt đầu.");
        });
    }
}

public sealed class FarmTaskActualSupplyInputValidator : AbstractValidator<FarmTaskActualSupplyInput>
{
    public FarmTaskActualSupplyInputValidator()
    {
        RuleFor(x => x.SupplyId)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn vật tư.")
            .NotEqual(Guid.Empty).WithMessage("Vật tư không hợp lệ.");

        When(x => x.ActualQuantity.HasValue, () =>
        {
            RuleFor(x => x.ActualQuantity!.Value)
                .Cascade(CascadeMode.Stop)
                .GreaterThan(0).WithMessage("Lượng vật tư thực dùng phải lớn hơn 0.")
                .PrecisionScale(18, 3, ignoreTrailingZeros: true)
                .WithMessage("Lượng vật tư tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.");
        });
    }
}

public sealed class RecordFarmTaskResultRequestValidator : AbstractValidator<RecordFarmTaskResultRequest>
{
    public RecordFarmTaskResultRequestValidator()
    {
        RuleFor(r => r.Status)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Vui lòng chọn trạng thái kết quả công việc.")
            .IsInEnum().WithMessage("Trạng thái công việc không hợp lệ.")
            .Must(s => s is FarmTaskStatus.Done or FarmTaskStatus.WaitingForSupply)
            .WithMessage("Trạng thái kết quả chỉ có thể là Hoàn thành hoặc Chờ bổ sung vật tư.");

        // BR-TSK-05: chuyển Hoàn thành bắt buộc có ngày thực hiện.
        When(r => r.Status == FarmTaskStatus.Done, () =>
        {
            RuleFor(r => r.PerformedDate)
                .NotNull().WithMessage("Vui lòng chọn ngày thực hiện thực tế.");
        });

        When(r => r.ActualDurationMinutes.HasValue, () =>
        {
            RuleFor(r => r.ActualDurationMinutes!.Value)
                .GreaterThan(0).WithMessage("Thời lượng thực tế phải lớn hơn 0 phút.");
        });

        RuleFor(r => r.AbnormalNote)
            .MaximumLength(2000).WithMessage("Ghi chú bất thường tối đa 2000 ký tự.");

        RuleFor(r => r.FieldNote)
            .MaximumLength(2000).WithMessage("Ghi chú hiện trường tối đa 2000 ký tự.");

        RuleFor(r => r.Reason)
            .MaximumLength(500).WithMessage("Lý do tối đa 500 ký tự.");

        RuleForEach(r => r.Supplies!)
            .SetValidator(new FarmTaskActualSupplyInputValidator())
            .When(r => r.Supplies is not null);

        RuleFor(r => r.Supplies)
            .Must(supplies => supplies is null || supplies
                .Where(s => s.SupplyId.HasValue && s.SupplyId.Value != Guid.Empty)
                .Select(s => s.SupplyId!.Value)
                .Distinct()
                .Count() == supplies.Count(s => s.SupplyId.HasValue && s.SupplyId.Value != Guid.Empty))
            .WithMessage("Mỗi vật tư chỉ được khai báo một dòng trong công việc.");
    }
}
