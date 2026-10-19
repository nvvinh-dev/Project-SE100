namespace HouseholdFarm.Api.Domain.Enums;

// Quyền bổ sung chỉ áp dụng cho Member; trong database lưu thành ba cột can_* của asp_net_users.
public enum ExtraPermission
{
    ManageStock,
    ManageFinance,
    CloseSeason,
}
