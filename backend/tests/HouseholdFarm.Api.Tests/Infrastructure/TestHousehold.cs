using HouseholdFarm.Api.Domain.Entities;

namespace HouseholdFarm.Api.Tests.Infrastructure;

/// <summary>Một hộ với đủ tài khoản của dữ liệu kiểm thử tối thiểu.</summary>
/// <param name="MemberWithAllPermissions">Thành viên hộ có đủ ba quyền bổ sung Kho, Tài chính, Chốt mùa vụ.</param>
/// <param name="Member">Thành viên hộ không có quyền bổ sung nào.</param>
public sealed record TestHousehold(
    Household Household,
    ApplicationUser Owner,
    ApplicationUser MemberWithAllPermissions,
    ApplicationUser Member,
    ApplicationUser Worker);
