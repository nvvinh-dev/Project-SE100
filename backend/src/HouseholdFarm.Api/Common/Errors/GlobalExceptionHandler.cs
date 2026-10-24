using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Common.Errors;

/// <summary>
/// Nơi duy nhất chuyển lỗi thành ProblemDetails (D-34): <see cref="AppException"/> theo mã lỗi của nó,
/// <see cref="DbUpdateConcurrencyException"/> thành <c>CONCURRENCY_CONFLICT</c>, lỗi còn lại thành <c>INTERNAL_ERROR</c>.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            AppException e => CreateProblem(e.Code, e.Message, e.Details, e.Errors),
            // BR-GEN-11: người khác đã sửa bản ghi sau lần đọc; giao diện yêu cầu tải lại.
            DbUpdateConcurrencyException => CreateProblem(
                ErrorCodes.ConcurrencyConflict,
                "Dữ liệu đã bị người khác thay đổi, vui lòng tải lại rồi thử lại."),
            _ => null,
        };

        if (problem is null)
        {
            // Từ .NET 10, lỗi đã được IExceptionHandler xử lý thì framework không tự ghi log nữa.
            // Không ghi query string vì có thể chứa số điện thoại khi tìm kiếm.
            logger.LogError(
                exception,
                "Lỗi không lường trước khi xử lý {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);

            // Không trả thông điệp, stack trace của lỗi cho client.
            problem = CreateProblem(ErrorCodes.InternalError, "Đã có lỗi hệ thống, vui lòng thử lại.");
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }

    private static ProblemDetails CreateProblem(
        string code,
        string detail,
        object? details = null,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Title = ErrorCodes.TitleOf(code),
            Status = ErrorCodes.StatusOf(code),
            Detail = detail,
        };
        problem.Extensions[AppProblemDetails.CodeKey] = code;

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        if (details is not null)
        {
            problem.Extensions["details"] = details;
        }

        return problem;
    }
}
