using FluentValidation;
using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Common.Validation;

// ValidationFilter tự chạy validator này cho mọi tham số lọc kế thừa PageQuery.
public sealed class PageQueryValidator : AbstractValidator<PageQuery>
{
    public PageQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Số trang phải từ 1 trở lên.");

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, PageQuery.MaxPageSize)
            .WithMessage($"Số dòng mỗi trang phải từ 1 đến {PageQuery.MaxPageSize}.");
    }
}
