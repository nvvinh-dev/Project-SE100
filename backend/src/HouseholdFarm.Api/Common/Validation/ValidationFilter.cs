using System.Text.Json;
using FluentValidation;
using HouseholdFarm.Api.Common.Errors;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HouseholdFarm.Api.Common.Validation;

/// <summary>
/// Kiểm tra định dạng dữ liệu trước khi vào controller. Lỗi đọc dữ liệu (sai kiểu, JSON hỏng, enum không có)
/// và lỗi của validator FluentValidation đều thành <c>VALIDATION_ERROR</c> kèm <c>errors</c> theo tên trường camelCase.
/// Controller không gọi validator bằng tay.
/// </summary>
/// <remarks>
/// Validator của lớp cha cũng được chạy, ví dụ <see cref="PageQueryValidator"/> cho mọi <c>&lt;Tên&gt;Query</c>
/// kế thừa <c>PageQuery</c>; validator của module không cần <c>Include</c> lại validator của lớp cha.
/// </remarks>
public sealed class ValidationFilter : IAsyncActionFilter
{
    // Thông báo của framework khi đọc dữ liệu thất bại là tiếng Anh và lộ chi tiết kỹ thuật, nên thay bằng câu chung.
    private const string InvalidFormatMessage = "Giá trị không đúng định dạng.";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var errors = new Dictionary<string, List<string>>();

        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count > 0)
            {
                Add(errors, ModelStateKeyToField(key), InvalidFormatMessage);
            }
        }

        // Dữ liệu đọc hỏng thì không chạy validator trên phần còn lại, tránh báo thêm lỗi do giá trị mặc định.
        if (errors.Count == 0)
        {
            var services = context.HttpContext.RequestServices;
            foreach (var argument in context.ActionArguments.Values.OfType<object>())
            {
                foreach (var validator in GetValidators(services, argument.GetType()))
                {
                    var result = await validator.ValidateAsync(
                        new ValidationContext<object>(argument),
                        context.HttpContext.RequestAborted);

                    foreach (var failure in result.Errors)
                    {
                        Add(errors, ToCamelCasePath(failure.PropertyName), failure.ErrorMessage);
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            throw AppException.Validation(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }

        await next();
    }

    private static IEnumerable<IValidator> GetValidators(IServiceProvider services, Type type)
    {
        for (var current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            foreach (var validator in services.GetServices(typeof(IValidator<>).MakeGenericType(current)))
            {
                yield return (IValidator)validator!;
            }
        }
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            errors[field] = messages = [];
        }

        if (!messages.Contains(message))
        {
            messages.Add(message);
        }
    }

    // Lỗi đọc JSON có khóa là đường dẫn JSON ("$.items[0].quantity", "$" khi lỗi ở gốc);
    // lỗi đọc query, route có khóa theo tên thuộc tính C# ("PageSize").
    private static string ModelStateKeyToField(string key) => key switch
    {
        "$" => string.Empty,
        _ when key.StartsWith("$.", StringComparison.Ordinal) => ToCamelCasePath(key[2..]),
        _ => ToCamelCasePath(key),
    };

    // "Items[0].Quantity" → "items[0].quantity", khớp tên trường JSON frontend gửi lên.
    private static string ToCamelCasePath(string path) =>
        string.Join('.', path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
