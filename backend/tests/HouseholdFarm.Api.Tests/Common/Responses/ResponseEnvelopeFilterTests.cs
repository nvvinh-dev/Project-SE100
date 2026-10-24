using System.Net;
using System.Text;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Tests.Common.Probe;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Common.Responses;

public class ResponseEnvelopeFilterTests(ProbeApiFactory factory) : IClassFixture<ProbeApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "Phản hồi một bản ghi được bọc { data, warnings }, không có meta")]
    public async Task SingleResult_IsWrappedWithEmptyWarnings()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.CreateVersion7();

        var response = await _client.GetAsync($"/api/probe/items/{id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync(ct);
        Assert.Equal(id, body.GetProperty("data").GetProperty("id").GetGuid());
        Assert.Equal(0, body.GetProperty("warnings").GetArrayLength());
        Assert.False(body.TryGetProperty("meta", out _));
    }

    [Fact(DisplayName = "Cảnh báo service ghi trong yêu cầu được trả trong warnings, theo thứ tự ghi")]
    public async Task Warnings_AreIncludedInResponse()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/probe/warnings", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync(ct);
        Assert.Equal("Thửa sau nhà", body.GetProperty("data").GetProperty("name").GetString());

        var warnings = body.GetProperty("warnings").EnumerateArray().ToList();
        Assert.Equal(2, warnings.Count);
        Assert.Equal(WarningCodes.LowStock, warnings[0].GetProperty("code").GetString());
        Assert.Equal(
            "Tồn Phân NPK sau khi xuất còn 20 kg, thấp hơn ngưỡng 30 kg.",
            warnings[0].GetProperty("message").GetString());
        Assert.Equal(WarningCodes.NegativeStock, warnings[1].GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Cảnh báo của yêu cầu trước không lọt sang yêu cầu sau")]
    public async Task Warnings_AreScopedToRequest()
    {
        var ct = TestContext.Current.CancellationToken;
        await _client.GetAsync("/api/probe/warnings", ct);

        var response = await _client.GetAsync($"/api/probe/items/{Guid.CreateVersion7()}", ct);

        var body = await response.ReadJsonAsync(ct);
        Assert.Equal(0, body.GetProperty("warnings").GetArrayLength());
    }

    [Fact(DisplayName = "PagedResult được bọc thành data là danh sách và meta gồm page, pageSize, totalCount")]
    public async Task PagedResult_IsWrappedWithMeta()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/probe/items?page=2&pageSize=3", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync(ct);
        var numbers = body.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("number").GetInt32());
        Assert.Equal([4, 5, 6], numbers);

        var meta = body.GetProperty("meta");
        Assert.Equal(2, meta.GetProperty("page").GetInt32());
        Assert.Equal(3, meta.GetProperty("pageSize").GetInt32());
        Assert.Equal(ProbeController.SampleTotalCount, meta.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, body.GetProperty("warnings").GetArrayLength());
    }

    [Fact(DisplayName = "Không truyền tham số trang thì lấy trang 1, 20 dòng")]
    public async Task PagedResult_DefaultsToFirstPageOf20()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/probe/items", ct);

        var meta = (await response.ReadJsonAsync(ct)).GetProperty("meta");
        Assert.Equal(1, meta.GetProperty("page").GetInt32());
        Assert.Equal(PageQuery.DefaultPageSize, meta.GetProperty("pageSize").GetInt32());
    }

    [Fact(DisplayName = "Tạo mới trả 201 kèm Location, body vẫn được bọc")]
    public async Task CreatedAtAction_IsWrapped()
    {
        var ct = TestContext.Current.CancellationToken;
        const string json = """
            {"name":"Thửa mới","areaM2":200,"soilType":"Clay","startDate":"2026-11-15","lines":[]}
            """;

        var response = await _client.PostAsync(
            "/api/probe/items",
            new StringContent(json, Encoding.UTF8, "application/json"),
            ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.EndsWith($"/api/probe/items/{ProbeController.Sample.Id}", response.Headers.Location?.ToString());
        var body = await response.ReadJsonAsync(ct);
        Assert.Equal("Thửa mới", body.GetProperty("data").GetProperty("name").GetString());
        Assert.Equal(0, body.GetProperty("warnings").GetArrayLength());
    }

    [Fact(DisplayName = "NoContent trả 204 không có body")]
    public async Task NoContent_IsNotWrapped()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.DeleteAsync($"/api/probe/items/{Guid.CreateVersion7()}", ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(ct));
    }
}
