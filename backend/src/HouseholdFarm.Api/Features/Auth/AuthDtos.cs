using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Auth;

public sealed record LoginRequest(string Phone, string Password);

/// <summary>Body chỉ có access token; refresh token nằm trong cookie <c>HttpOnly</c> (D-12).</summary>
public sealed record AccessTokenResponse(string AccessToken);

/// <summary>Người đang đăng nhập; frontend dùng để ẩn, hiện chức năng theo vai trò và quyền bổ sung.</summary>
/// <param name="Household">Để trống với Quản trị hệ thống.</param>
public sealed record CurrentUserResponse(
    Guid Id,
    string FullName,
    string Phone,
    UserRole Role,
    CurrentUserHousehold? Household,
    bool CanManageStock,
    bool CanManageFinance,
    bool CanCloseSeason);

public sealed record CurrentUserHousehold(string Code, string Name);

/// <summary>Một phiên đăng nhập vừa cấp. Controller đặt refresh token vào cookie, body chỉ trả access token.</summary>
public sealed record AuthSession(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt);

/// <summary>Đăng ký Chủ hộ và tạo hộ mới cùng lúc (FR-AUTH-01).</summary>
/// <param name="ContactInfo">Thông tin liên hệ của hộ, không bắt buộc.</param>
public sealed record RegisterRequest(
    string FullName,
    string Phone,
    string Password,
    string HouseholdName,
    string ProductionAddress,
    string? ContactInfo);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UpdateProfileRequest(string FullName, string Phone);
