using HouseholdFarm.Api.Common.Persistence;

namespace HouseholdFarm.Api.Tests.Common.Persistence;

public class SearchPatternsTests
{
    [Theory(DisplayName = "Mẫu tìm kiếm bỏ khoảng trắng hai đầu, thoát ký tự đặc biệt của LIKE")]
    [InlineData("  bờ kênh ", "%bờ kênh%")]
    [InlineData("50%", @"%50\%%")]
    [InlineData("TD_1", @"%TD\_1%")]
    [InlineData(@"a\b", @"%a\\b%")]
    public void Contains_EscapesWildcards(string search, string expected) =>
        Assert.Equal(expected, SearchPatterns.Contains(search));
}
