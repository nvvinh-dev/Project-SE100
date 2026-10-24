namespace HouseholdFarm.Api.Common.Responses;

// Scoped: mỗi yêu cầu một danh sách, dùng chung giữa các service được gọi lồng trong yêu cầu đó.
public sealed class WarningCollector : IWarningCollector
{
    private readonly List<ResponseWarning> _warnings = [];

    public IReadOnlyList<ResponseWarning> Warnings => _warnings;

    public void Add(string code, string message) => _warnings.Add(new ResponseWarning(code, message));
}
