namespace HouseholdFarm.Api.Domain.Entities;

// Owned type của Household: một class riêng trong code nhưng các cột nằm chung bảng households.
public class HouseholdSettings
{
    public bool AllowNegativeStock { get; set; }

    public bool AllowOverlappingSeasons { get; set; }
}
