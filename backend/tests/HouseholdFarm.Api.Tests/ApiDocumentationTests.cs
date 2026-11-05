using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HouseholdFarm.Api.Tests;

public class ApiDocumentationTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory(DisplayName = "Môi trường Development mở tài liệu OpenAPI và Swagger UI")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    public async Task Development_ServesApiDocumentation(string path)
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "Tài liệu OpenAPI liệt kê API đăng nhập và khai báo access token để thử API trên Swagger UI")]
    public async Task Development_DocumentListsAuthEndpointsAndBearerScheme()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Development")).CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json", ct);

        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/auth/me", out _));
        var scheme = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());
    }

    [Theory(DisplayName = "Môi trường Production không mở tài liệu OpenAPI và Swagger UI")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/swagger/index.html")]
    public async Task Production_HidesApiDocumentation(string path)
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
