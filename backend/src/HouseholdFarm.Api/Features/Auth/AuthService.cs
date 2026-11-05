using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Auth;

public sealed class AuthService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    AccessTokenIssuer accessTokenIssuer,
    JwtSettings jwtSettings,
    ICurrentUser currentUser,
    IAppClock clock) : IAuthService
{
    public async Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        // Validator đã bảo đảm số chuẩn hóa được (BR-USR-01).
        var phone = PhoneNumbers.Normalize(request.Phone)!;
        var user = await userManager.FindByNameAsync(phone) ?? throw InvalidCredentials();

        // BR-USR-15: đang khóa tạm thì từ chối cả khi đúng mật khẩu, nên kiểm tra trước mật khẩu.
        // Identity so lockout_end với giờ hệ thống, không qua IAppClock.
        if (await userManager.IsLockedOutAsync(user))
        {
            throw TooManyAttempts(user);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Sai liên tiếp đến ngưỡng thì Identity tự đặt khóa tạm và đưa bộ đếm về 0.
            await userManager.AccessFailedAsync(user);
            throw InvalidCredentials();
        }

        // BR-USR-03: chỉ báo tài khoản bị khóa khi đã đúng mật khẩu, để người ngoài không dò được tài khoản nào đang bị khóa.
        if (user.IsLocked)
        {
            throw new AppException(ErrorCodes.AccountLocked, CurrentUserMiddleware.AccountLockedMessage);
        }

        // BR-USR-15: đăng nhập thành công thì bộ đếm sai về 0.
        await userManager.ResetAccessFailedCountAsync(user);

        return await StartSessionAsync(user, ct);
    }

    public async Task<CurrentUserResponse> GetMeAsync(CancellationToken ct)
    {
        currentUser.EnsureAuthenticated();

        return await db.Users
            .AsNoTracking()
            .Where(u => u.Id == currentUser.UserId)
            .Select(u => new CurrentUserResponse(
                u.Id,
                u.FullName,
                u.PhoneNumber!,
                u.Role,
                u.Household == null ? null : new CurrentUserHousehold(u.Household.Code, u.Household.Name),
                u.CanManageStock,
                u.CanManageFinance,
                u.CanCloseSeason))
            .SingleAsync(ct);
    }

    // Cấp một phiên đăng nhập (D-10, D-12); đăng ký và làm mới phiên dùng lại hàm này.
    private async Task<AuthSession> StartSessionAsync(ApplicationUser user, CancellationToken ct)
    {
        var refreshToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var refreshTokenExpiresAt = clock.UtcNow + jwtSettings.RefreshTokenLifetime;

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            ExpiresAt = refreshTokenExpiresAt,
        });
        await db.SaveChangesAsync(ct);

        return new AuthSession(accessTokenIssuer.Create(user.Id), refreshToken, refreshTokenExpiresAt);
    }

    // Chỉ lưu giá trị băm để lộ database cũng không dùng lại được token. SHA-256 không cần muối như mật khẩu,
    // vì token là 32 byte ngẫu nhiên, không dò ngược được.
    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    // BR-USR-03: sai số điện thoại hay sai mật khẩu cùng một lỗi, không cho biết sai phần nào.
    private static AppException InvalidCredentials() =>
        new(ErrorCodes.InvalidCredentials, "Số điện thoại hoặc mật khẩu không đúng.");

    private AppException TooManyAttempts(ApplicationUser user)
    {
        var minutesLeft = Math.Max(1, (int)Math.Ceiling((user.LockoutEnd!.Value - clock.UtcNow).TotalMinutes));
        return new AppException(
            ErrorCodes.TooManyAttempts,
            $"Đăng nhập sai quá nhiều lần nên tài khoản đang bị khóa tạm. Thử lại sau {minutesLeft} phút.");
    }
}
