using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace HouseholdFarm.Api.Tests.Infrastructure;

public static class HttpResponseAssertions
{
    /// <summary>Đọc body JSON của phản hồi.</summary>
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ct);

    /// <summary>
    /// Kiểm tra phản hồi lỗi là ProblemDetails với đúng mã HTTP và đúng <c>code</c>; trả về body để kiểm tra thêm.
    /// </summary>
    public static async Task<JsonElement> AssertProblemAsync(
        this HttpResponseMessage response,
        HttpStatusCode status,
        string code,
        CancellationToken ct)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.ReadJsonAsync(ct);
        Assert.Equal(code, body.GetProperty("code").GetString());
        return body;
    }
}
