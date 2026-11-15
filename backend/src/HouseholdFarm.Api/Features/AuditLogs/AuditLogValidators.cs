using FluentValidation;
using HouseholdFarm.Api.Common.Auditing;

namespace HouseholdFarm.Api.Features.AuditLogs;

public sealed class AuditLogQueryValidator : AbstractValidator<AuditLogQuery>
{
    public AuditLogQueryValidator()
    {
        // page, pageSize do PageQueryValidator chung kiểm tra. Nhật ký luôn sắp mới nhất trước nên không nhận sort.
        RuleFor(q => q.Sort).Null().WithMessage("Nhật ký thao tác luôn sắp xếp mới nhất trước.");
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .When(q => q.From is not null && q.To is not null)
            .WithMessage("Ngày kết thúc phải từ ngày bắt đầu trở về sau.");
        RuleFor(q => q.Action)
            .Must(action => action is null || AuditActions.IsDefined(action))
            .WithMessage("Loại hành động không hợp lệ.");
    }
}
