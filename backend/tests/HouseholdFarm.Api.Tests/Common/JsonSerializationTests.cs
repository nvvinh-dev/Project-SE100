using System.Net;
using System.Text;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Tests.Common.Probe;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Common;

public class JsonSerializationTests(ProbeApiFactory factory) : IClassFixture<ProbeApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "Phản hồi: tên trường camelCase, enum dạng chuỗi, DateOnly yyyy-MM-dd, thời điểm UTC có Z")]
    public async Task Response_UsesAgreedJsonFormat()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"/api/probe/items/{ProbeController.Sample.Id}", ct);

        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(
            ["id", "name", "areaM2", "soilType", "startDate", "createdAt", "version"],
            data.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Loam", data.GetProperty("soilType").GetString());
        Assert.Equal("2026-10-01", data.GetProperty("startDate").GetString());
        Assert.Equal("2026-10-01T07:30:00Z", data.GetProperty("createdAt").GetString());
        Assert.Equal(1500.5m, data.GetProperty("areaM2").GetDecimal());
        Assert.Equal(83921u, data.GetProperty("version").GetUInt32());
    }

    [Fact(DisplayName = "Request: đọc được enum dạng chuỗi và ngày dạng yyyy-MM-dd")]
    public async Task Request_ReadsEnumNameAndDateOnly()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await PostAsync(
            """{"name":"Thửa mới","areaM2":200,"soilType":"RedBasalt","startDate":"2026-11-15","lines":[]}""",
            ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("RedBasalt", data.GetProperty("soilType").GetString());
        Assert.Equal("2026-11-15", data.GetProperty("startDate").GetString());
    }

    [Theory(DisplayName = "Request: enum gửi dạng số hoặc tên không có thì bị từ chối ở đúng trường")]
    [InlineData("3")]
    [InlineData("\"3\"")]
    [InlineData("\"Peat\"")]
    public async Task Request_RejectsInvalidEnum(string soilType)
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await PostAsync(
            $$"""{"name":"Thửa mới","areaM2":200,"soilType":{{soilType}},"startDate":"2026-11-15","lines":[]}""",
            ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal(["soilType"], body.GetProperty("errors").EnumerateObject().Select(p => p.Name));
    }

    [Fact(DisplayName = "Request: ngày sai dạng yyyy-MM-dd bị từ chối ở đúng trường")]
    public async Task Request_RejectsDateInOtherFormat()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await PostAsync(
            """{"name":"Thửa mới","areaM2":200,"soilType":"Loam","startDate":"15/11/2026","lines":[]}""",
            ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal(["startDate"], body.GetProperty("errors").EnumerateObject().Select(p => p.Name));
    }

    private Task<HttpResponseMessage> PostAsync(string json, CancellationToken ct) =>
        _client.PostAsync("/api/probe/items", new StringContent(json, Encoding.UTF8, "application/json"), ct);
}
