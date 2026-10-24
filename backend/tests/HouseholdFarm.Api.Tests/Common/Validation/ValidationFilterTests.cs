using System.Net;
using System.Text;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Tests.Common.Probe;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Common.Validation;

public class ValidationFilterTests(ProbeApiFactory factory) : IClassFixture<ProbeApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact(DisplayName = "Lỗi validator trả 400 VALIDATION_ERROR, errors theo tên trường camelCase kể cả trường lồng")]
    public async Task ValidatorErrors_ReturnFieldErrors()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await PostAsync(
            """{"name":"","areaM2":0,"soilType":"Loam","startDate":"2026-11-15","lines":[{"quantity":2},{"quantity":0}]}""",
            ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal("Dữ liệu không hợp lệ", body.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("detail").GetString()));

        var errors = ReadErrors(body);
        Assert.Equal(["areaM2", "lines[1].quantity", "name"], errors.Keys.Order());
        Assert.Equal(["Vui lòng nhập tên."], errors["name"]);
        Assert.Equal(["Diện tích phải lớn hơn 0."], errors["areaM2"]);
        Assert.Equal(["Số lượng phải lớn hơn 0."], errors["lines[1].quantity"]);
    }

    [Fact(DisplayName = "Quy tắc không viết WithMessage vẫn có thông báo mặc định tiếng Việt")]
    public async Task ValidatorWithoutMessage_UsesVietnameseDefault()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await PostAsync(
            """{"name":"Thửa","areaM2":1,"soilType":"Loam","startDate":"2026-11-15","lines":[],"note":"quá mười ký tự"}""",
            ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        var message = Assert.Single(ReadErrors(body)["note"]);
        Assert.StartsWith("Độ dài tối đa", message);
    }

    [Fact(DisplayName = "Dữ liệu hợp lệ thì vào được controller")]
    public async Task ValidRequest_ReachesController()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await PostAsync(
            """{"name":"Thửa","areaM2":1,"soilType":"Loam","startDate":"2026-11-15","lines":[{"quantity":1}]}""",
            ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "Dữ liệu đọc không được (sai kiểu) thì chỉ báo trường đó, không chạy validator trên phần còn lại")]
    public async Task BindingError_SkipsValidators()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await PostAsync(
            """{"name":"","areaM2":"nhiều","soilType":"Loam","startDate":"2026-11-15","lines":[]}""",
            ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        var errors = ReadErrors(body);
        Assert.Equal(["areaM2"], errors.Keys);
        Assert.Equal(["Giá trị không đúng định dạng."], errors["areaM2"]);
    }

    [Fact(DisplayName = "JSON hỏng trả 400 VALIDATION_ERROR")]
    public async Task MalformedJson_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await PostAsync("""{"name": "Thửa", """, ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.NotEmpty(ReadErrors(body));
    }

    [Theory(DisplayName = "Tham số phân trang ngoài khoảng của PageQuery bị từ chối, kể cả với Query của module kế thừa")]
    [InlineData("page=0", "page", "Số trang phải từ 1 trở lên.")]
    [InlineData("pageSize=0", "pageSize", "Số dòng mỗi trang phải từ 1 đến 100.")]
    [InlineData("pageSize=101", "pageSize", "Số dòng mỗi trang phải từ 1 đến 100.")]
    [InlineData("page=abc", "page", "Giá trị không đúng định dạng.")]
    public async Task PageQueryOutOfRange_ReturnsFieldError(string queryString, string field, string message)
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync($"/api/probe/items?{queryString}", ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        var errors = ReadErrors(body);
        Assert.Equal([field], errors.Keys);
        Assert.Equal([message], errors[field]);
    }

    [Fact(DisplayName = "pageSize bằng 100 vẫn được chấp nhận")]
    public async Task PageSizeAtMax_IsAccepted()
    {
        var response = await _client.GetAsync("/api/probe/items?pageSize=100", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostAsync(string json, CancellationToken ct) =>
        _client.PostAsync("/api/probe/items", new StringContent(json, Encoding.UTF8, "application/json"), ct);

    private static Dictionary<string, string[]> ReadErrors(JsonElement body) =>
        body.GetProperty("errors").Deserialize<Dictionary<string, string[]>>()!;
}
