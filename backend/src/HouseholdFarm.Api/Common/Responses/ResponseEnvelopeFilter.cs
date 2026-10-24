using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HouseholdFarm.Api.Common.Responses;

/// <summary>
/// Bọc kết quả thành công của controller thành <see cref="ResponseEnvelope"/>, kèm cảnh báo của yêu cầu (D-34).
/// Controller chỉ <c>return Ok(dto)</c>; định dạng phản hồi được quyết định ở đúng chỗ này.
/// </summary>
/// <remarks>
/// Không bọc lỗi (ProblemDetails), phản hồi không có body (<c>NoContent()</c>) và file xuất báo cáo.
/// </remarks>
public sealed class ResponseEnvelopeFilter(IWarningCollector warnings) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is not ObjectResult result
            || result.Value is ProblemDetails or ResponseEnvelope
            || !IsSuccess(result.StatusCode ?? StatusCodes.Status200OK))
        {
            return;
        }

        result.Value = result.Value is IPagedResult paged
            ? new ResponseEnvelope(paged.Items, paged.Meta, warnings.Warnings)
            : new ResponseEnvelope(result.Value, Meta: null, warnings.Warnings);

        // ActionResult<T> đặt DeclaredType là T; giữ nguyên thì bộ ghi JSON tuần tự hóa envelope theo kiểu T.
        result.DeclaredType = typeof(ResponseEnvelope);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    private static bool IsSuccess(int statusCode) => statusCode is >= 200 and < 300;
}
