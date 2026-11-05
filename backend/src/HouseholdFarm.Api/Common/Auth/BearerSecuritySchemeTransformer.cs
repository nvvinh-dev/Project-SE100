using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Khai báo access token trong tài liệu OpenAPI, để Swagger UI có nút nhập token và gửi kèm header
/// <c>Authorization: Bearer</c> khi thử API.
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.AddComponent<IOpenApiSecurityScheme>(SchemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token nhận được từ POST /api/auth/login.",
        });
        document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, document)] = [] }];
        return Task.CompletedTask;
    }
}
