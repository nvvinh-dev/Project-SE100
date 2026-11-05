using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HouseholdFarm.Api.Features.Auth;

public sealed class AuthService(
    AppDbContext db,
    UserManager<ApplicationUser> userManager,
    AccessTokenIssuer accessTokenIssuer,
    JwtSettings jwtSettings,
    RefreshTokenRevoker refreshTokenRevoker,
    ICurrentUser currentUser,
    IAppClock clock) : IAuthService
{
    // Mã hộ ngẫu nhiên hiếm khi trùng; vẫn giới hạn số lần thử để không lặp mãi khi database có vấn đề.
    private const int MaxHouseholdCodeAttempts = 10;

    public async Task<CurrentUserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        // Validator đã bảo đảm số chuẩn hóa được (BR-USR-01).
        var phone = PhoneNumbers.Normalize(request.Phone)!;
        await EnsurePhoneUnusedAsync(phone, exceptUserId: null, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // BR-CAT-02; hai công tắc cài đặt hộ mặc định tắt (FR-HH-08).
        var household = new Household
        {
            Code = await NewHouseholdCodeAsync(ct),
            Name = request.HouseholdName.Trim(),
            ProductionAddress = request.ProductionAddress.Trim(),
            ContactInfo = string.IsNullOrWhiteSpace(request.ContactInfo) ? null : request.ContactInfo.Trim(),
        };
        db.Households.Add(household);

        // BR-USR-07: người đăng ký là Chủ hộ duy nhất của hộ mới.
        var owner = new ApplicationUser
        {
            HouseholdId = household.Id,
            FullName = request.FullName.Trim(),
            Role = UserRole.Owner,
            UserName = phone,
            PhoneNumber = phone,
        };

        // UserManager lưu qua cùng AppDbContext nên hộ và tài khoản được ghi trong cùng giao dịch.
        await SaveUserAsync(() => userManager.CreateAsync(owner, request.Password));
        await transaction.CommitAsync(ct);

        // UC-AUTH-01: chưa bắt đầu phiên, người dùng đăng nhập ở bước sau.
        return new CurrentUserResponse(
            owner.Id,
            owner.FullName,
            phone,
            owner.Role,
            new CurrentUserHousehold(household.Code, household.Name),
            CanManageStock: false,
            CanManageFinance: false,
            CanCloseSeason: false);
    }

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

        var (session, _) = StartSession(user.Id);
        await db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken ct)
    {
        if (refreshToken is null)
        {
            throw InvalidRefreshToken();
        }

        var tokenHash = HashRefreshToken(refreshToken);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Khóa dòng token: hai yêu cầu làm mới cùng lúc bằng một token thì yêu cầu sau thấy token đã bị thu hồi,
        // không cấp được hai token mới từ một token cũ.
        var current = (await db.RefreshTokens
                .FromSql($"SELECT * FROM refresh_tokens WHERE token_hash = {tokenHash} FOR UPDATE")
                .ToListAsync(ct))
            .SingleOrDefault()
            ?? throw InvalidRefreshToken();

        if (current.RevokedAt is not null)
        {
            // BR-USR-05: token đã thu hồi bị gửi lại là dấu hiệu token bị đánh cắp, kết thúc mọi phiên của người đó.
            await refreshTokenRevoker.RevokeAllAsync(current.UserId, ct);
            await transaction.CommitAsync(ct);
            throw InvalidRefreshToken();
        }

        var now = clock.UtcNow;
        if (current.ExpiresAt <= now)
        {
            throw InvalidRefreshToken();
        }

        // BR-USR-04, BR-GEN-04: khóa tài khoản đã thu hồi token; vẫn kiểm tra phòng token cấp sau khi khóa.
        var isLocked = await db.Users.Where(u => u.Id == current.UserId).Select(u => u.IsLocked).SingleAsync(ct);
        if (isLocked)
        {
            throw InvalidRefreshToken();
        }

        // BR-USR-05: thu hồi token cũ, cấp token mới, ghi token thay thế.
        var (session, replacement) = StartSession(current.UserId);
        current.RevokedAt = now;
        current.ReplacedById = replacement.Id;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return session;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (refreshToken is null)
        {
            return;
        }

        // BR-USR-04: chỉ thu hồi token của phiên này, các thiết bị khác vẫn đăng nhập.
        var tokenHash = HashRefreshToken(refreshToken);
        var now = clock.UtcNow;
        await db.RefreshTokens
            .Where(t => t.TokenHash == tokenHash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        currentUser.EnsureAuthenticated();
        var user = await userManager.FindByIdAsync(currentUser.UserId.ToString())
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản của người đang đăng nhập.");

        // BR-USR-17: sai mật khẩu hiện tại không tính vào bộ đếm khóa tạm, nên không gọi AccessFailedAsync.
        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            throw new AppException(ErrorCodes.InvalidCredentials, "Mật khẩu hiện tại không đúng.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await SaveUserAsync(() => userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword));

        // BR-USR-04: kết thúc mọi phiên, kể cả phiên đang dùng; người dùng đăng nhập lại bằng mật khẩu mới.
        await refreshTokenRevoker.RevokeAllAsync(user.Id, ct);
        await transaction.CommitAsync(ct);
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

    public async Task<CurrentUserResponse> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        currentUser.EnsureAuthenticated();

        // Validator đã bảo đảm số chuẩn hóa được (BR-USR-01).
        var phone = PhoneNumbers.Normalize(request.Phone)!;
        await EnsurePhoneUnusedAsync(phone, exceptUserId: currentUser.UserId, ct);

        var user = await userManager.FindByIdAsync(currentUser.UserId.ToString())
            ?? throw new InvalidOperationException("Không tìm thấy tài khoản của người đang đăng nhập.");
        user.FullName = request.FullName.Trim();
        // Đăng nhập tìm theo user_name nên đổi cùng lúc với phone_number; UpdateAsync tự cập nhật normalized_user_name.
        user.UserName = phone;
        user.PhoneNumber = phone;
        await SaveUserAsync(() => userManager.UpdateAsync(user));

        return await GetMeAsync(ct);
    }

    // BR-USR-02: so với mọi tài khoản, kể cả tài khoản bị khóa và Quản trị hệ thống.
    private async Task EnsurePhoneUnusedAsync(string phone, Guid? exceptUserId, CancellationToken ct)
    {
        var normalizedUserName = userManager.NormalizeName(phone);
        if (await db.Users.AnyAsync(u => u.NormalizedUserName == normalizedUserName && u.Id != exceptUserId, ct))
        {
            throw PhoneAlreadyUsed();
        }
    }

    // Ghi tài khoản qua UserManager. Kiểm tra trùng số ở trên có thể lọt khi hai yêu cầu cùng lúc;
    // khi đó Identity hoặc chỉ mục duy nhất của user_name chặn lại, vẫn báo PHONE_ALREADY_USED.
    private async Task SaveUserAsync(Func<Task<IdentityResult>> save)
    {
        IdentityResult result;
        try
        {
            result = await save();
        }
        catch (DbUpdateException ex) when (IsUserNameViolation(ex))
        {
            throw PhoneAlreadyUsed();
        }

        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            throw PhoneAlreadyUsed();
        }

        // Validator đã kiểm tra mật khẩu theo cùng chính sách với Identity, lỗi còn lại là lỗi lập trình.
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity từ chối lưu tài khoản: " + string.Join(", ", result.Errors.Select(e => e.Code)));
        }
    }

    private bool IsUserNameViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            return false;
        }

        var userNameIndex = db.Model.FindEntityType(typeof(ApplicationUser))!
            .GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(ApplicationUser.NormalizedUserName)));
        return postgres.ConstraintName == userNameIndex.GetDatabaseName();
    }

    // BR-CAT-02: HH + 6 chữ số. Sinh ngẫu nhiên thay vì tăng dần để mã không cho biết hệ thống có bao nhiêu hộ.
    private async Task<string> NewHouseholdCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < MaxHouseholdCodeAttempts; attempt++)
        {
            var code = $"HH{RandomNumberGenerator.GetInt32(1_000_000):D6}";
            if (!await db.Households.AnyAsync(h => h.Code == code, ct))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Không sinh được mã hộ chưa dùng.");
    }

    // Cấp một phiên đăng nhập (D-10, D-12); bên gọi tự lưu để gộp với thay đổi khác trong cùng lần lưu.
    private (AuthSession Session, RefreshToken Token) StartSession(Guid userId)
    {
        var refreshToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = HashRefreshToken(refreshToken),
            ExpiresAt = clock.UtcNow + jwtSettings.RefreshTokenLifetime,
        };
        db.RefreshTokens.Add(token);

        return (new AuthSession(accessTokenIssuer.Create(userId), refreshToken, token.ExpiresAt), token);
    }

    // Chỉ lưu giá trị băm để lộ database cũng không dùng lại được token. SHA-256 không cần muối như mật khẩu,
    // vì token là 32 byte ngẫu nhiên, không dò ngược được.
    private static string HashRefreshToken(string refreshToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    // BR-USR-03: sai số điện thoại hay sai mật khẩu cùng một lỗi, không cho biết sai phần nào.
    private static AppException InvalidCredentials() =>
        new(ErrorCodes.InvalidCredentials, "Số điện thoại hoặc mật khẩu không đúng.");

    private static AppException InvalidRefreshToken() =>
        new(ErrorCodes.InvalidRefreshToken, "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.");

    private static AppException PhoneAlreadyUsed() =>
        new(ErrorCodes.PhoneAlreadyUsed, "Số điện thoại này đã được dùng cho một tài khoản khác.");

    private AppException TooManyAttempts(ApplicationUser user)
    {
        var minutesLeft = Math.Max(1, (int)Math.Ceiling((user.LockoutEnd!.Value - clock.UtcNow).TotalMinutes));
        return new AppException(
            ErrorCodes.TooManyAttempts,
            $"Đăng nhập sai quá nhiều lần nên tài khoản đang bị khóa tạm. Thử lại sau {minutesLeft} phút.");
    }
}
