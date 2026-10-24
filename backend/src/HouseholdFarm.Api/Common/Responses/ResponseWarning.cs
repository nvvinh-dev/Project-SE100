namespace HouseholdFarm.Api.Common.Responses;

/// <param name="Code">Hằng số trong <see cref="WarningCodes"/>.</param>
/// <param name="Message">Câu tiếng Việt, frontend hiển thị nguyên văn.</param>
public sealed record ResponseWarning(string Code, string Message);
