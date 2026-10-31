using System.Diagnostics;

namespace HouseholdFarm.Api.Common.Errors;

/// <summary>
/// Bổ sung chung cho mọi ProblemDetails, kể cả ProblemDetails do framework tự sinh không qua
/// <see cref="GlobalExceptionHandler"/> (route không khớp, xác thực JWT từ chối).
/// </summary>
public static class AppProblemDetails
{
    public const string CodeKey = "code";

    // Framework chỉ biết mã HTTP; gắn mã lỗi tương ứng để frontend xử lý theo code như mọi lỗi khác.
    // Mã HTTP không có mã lỗi tương ứng (405, 415) chỉ xảy ra khi frontend gọi sai, giữ nguyên không gắn code.
    private static readonly Dictionary<int, (string Code, string Detail)> _frameworkErrors = new()
    {
        [StatusCodes.Status400BadRequest] = (ErrorCodes.ValidationError, "Yêu cầu không hợp lệ."),
        [StatusCodes.Status401Unauthorized] = (ErrorCodes.Unauthenticated, "Chưa đăng nhập hoặc phiên đăng nhập đã hết hạn."),
        [StatusCodes.Status403Forbidden] = (ErrorCodes.Forbidden, "Tài khoản không có quyền thực hiện thao tác này."),
        [StatusCodes.Status404NotFound] = (ErrorCodes.NotFound, "Không tìm thấy địa chỉ được yêu cầu."),
    };

    /// <summary>Dùng làm <c>ProblemDetailsOptions.CustomizeProblemDetails</c>.</summary>
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;

        // traceId để tra log khi người dùng báo lỗi.
        problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);

        if (!problem.Extensions.ContainsKey(CodeKey)
            && problem.Status is { } status
            && _frameworkErrors.TryGetValue(status, out var error))
        {
            problem.Extensions[CodeKey] = error.Code;
            problem.Title = ErrorCodes.TitleOf(error.Code);
            problem.Detail ??= error.Detail;
        }
    }
}
