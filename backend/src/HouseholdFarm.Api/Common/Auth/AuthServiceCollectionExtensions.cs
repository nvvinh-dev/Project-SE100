using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HouseholdFarm.Api.Common.Auth;

public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Tài khoản (Identity), xác thực JWT Bearer, cookie refresh token và người dùng hiện tại nạp mỗi yêu cầu.
    /// Cấu hình đọc lúc dùng để test thay được bằng cấu hình riêng.
    /// </summary>
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services)
    {
        // Chỉ phần tài khoản của Identity: không có bảng vai trò, không đăng nhập bằng cookie của Identity.
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<AppDbContext>();
        services.AddOptions<IdentityOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            // BR-USR-15: khóa tạm bằng lockout có sẵn của Identity; tài khoản mới luôn bật lockout.
            options.Lockout.MaxFailedAccessAttempts = configuration.GetValue<int>("Auth:LockoutMaxFailedAttempts");
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(configuration.GetValue<int>("Auth:LockoutMinutes"));
            options.Lockout.AllowedForNewUsers = true;
        });

        services.AddSingleton<JwtSettings>();
        services.AddSingleton<AccessTokenIssuer>();
        services.AddSingleton<RefreshTokenCookie>();

        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtSettings>((options, jwt) =>
        {
            // Giữ nguyên tên claim sub, không đổi sang tên dài kiểu ClaimTypes.NameIdentifier.
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                // Chưa khai báo khóa ký thì mọi token đều bị từ chối (401), không làm hỏng các yêu cầu không cần đăng nhập.
                IssuerSigningKey = jwt.FindSigningKey(),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                NameClaimType = JwtRegisteredClaimNames.Sub,
            };
        });
        services.AddAuthorization();

        return services;
    }
}
