using System.Net;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Tests.Common.Probe;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace HouseholdFarm.Api.Tests.Common.Errors;

public class GlobalExceptionHandlerTests(ProbeApiFactory factory) : IClassFixture<ProbeApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "AppException thành ProblemDetails có title, status, detail, code, details, traceId")]
    public async Task AppException_ReturnsProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/probe/errors/insufficient-stock", ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientStock, ct);
        Assert.Equal("Không đủ tồn kho", body.GetProperty("title").GetString());
        Assert.Equal(422, body.GetProperty("status").GetInt32());
        Assert.Equal("Phân NPK chỉ còn 5 kg, không đủ để xuất 8 kg.", body.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
        Assert.False(body.TryGetProperty("errors", out _));

        var details = body.GetProperty("details");
        Assert.Equal(ProbeController.Sample.Id, details.GetProperty("supplyId").GetGuid());
        Assert.Equal(5, details.GetProperty("available").GetInt32());
        Assert.Equal(8, details.GetProperty("requested").GetInt32());
        // ProblemDetails dùng cấu hình JSON riêng với controller; enum vẫn phải là chuỗi.
        Assert.Equal("Kilogram", details.GetProperty("unit").GetString());
    }

    [Fact(DisplayName = "BR-GEN-14: CONFIRMATION_REQUIRED trả 422 kèm details.items gồm id, label")]
    public async Task ConfirmationRequired_ReturnsItems()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/probe/errors/confirmation-required", ct);

        var body = await response.AssertProblemAsync(
            HttpStatusCode.UnprocessableEntity,
            ErrorCodes.ConfirmationRequired,
            ct);
        var item = Assert.Single(body.GetProperty("details").GetProperty("items").EnumerateArray());
        Assert.Equal(ProbeController.Sample.Id, item.GetProperty("id").GetGuid());
        Assert.Equal("Tưới nước - 28/09/2026", item.GetProperty("label").GetString());
    }

    [Fact(DisplayName = "AppException.NotFound trả 404 NOT_FOUND với câu nêu tên bản ghi")]
    public async Task NotFound_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/probe/errors/not-found", ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        Assert.Equal("Không tìm thấy vật tư.", body.GetProperty("detail").GetString());
    }

    [Fact(DisplayName = "BR-GEN-11: DbUpdateConcurrencyException thành 409 CONCURRENCY_CONFLICT")]
    public async Task ConcurrencyException_Returns409()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/probe/errors/concurrency", ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "Lỗi không lường trước trả 500 INTERNAL_ERROR câu chung, không lộ chi tiết, có ghi log lỗi")]
    public async Task UnexpectedException_Returns500WithoutDetails()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/probe/errors/unexpected", ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.InternalServerError, ErrorCodes.InternalError, ct);
        Assert.Equal("Đã có lỗi hệ thống, vui lòng thử lại.", body.GetProperty("detail").GetString());

        var raw = body.GetRawText();
        Assert.DoesNotContain(ProbeController.UnexpectedMessage, raw);
        Assert.DoesNotContain(nameof(InvalidOperationException), raw);
        Assert.DoesNotContain(nameof(ProbeController), raw);

        Assert.Contains(factory.ExceptionLogger.Entries, e => e.Level == LogLevel.Error
            && e.Exception is InvalidOperationException { Message: ProbeController.UnexpectedMessage });
    }

    [Fact(DisplayName = "Đường dẫn API không tồn tại trả ProblemDetails 404 NOT_FOUND có traceId")]
    public async Task UnknownRoute_Returns404ProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/khong-ton-tai", ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        Assert.Equal("Không tìm thấy", body.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }
}
