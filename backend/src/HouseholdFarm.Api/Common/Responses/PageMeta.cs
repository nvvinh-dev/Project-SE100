namespace HouseholdFarm.Api.Common.Responses;

/// <summary>Thông tin trang trả ở <c>meta</c> của phản hồi danh sách.</summary>
public sealed record PageMeta(int Page, int PageSize, int TotalCount);
