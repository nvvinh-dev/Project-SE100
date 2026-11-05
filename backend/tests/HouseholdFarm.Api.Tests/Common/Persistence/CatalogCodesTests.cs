using HouseholdFarm.Api.Common.Persistence;

namespace HouseholdFarm.Api.Tests.Common.Persistence;

public class CatalogCodesTests
{
    [Fact(DisplayName = "BR-CAT-01: hộ chưa có mã đúng mẫu thì bắt đầu từ 001")]
    public void Next_WithoutMatchingCodes_StartsFromOne() =>
        Assert.Equal("TD001", CatalogCodes.Next(["TD-SAU-NHA", "VT005", "TD12"], "TD", 20));

    [Fact(DisplayName = "BR-CAT-01: số tiếp theo là số lớn nhất trong các mã đúng mẫu + 1, không lấp chỗ trống")]
    public void Next_UsesLargestNumber() =>
        Assert.Equal("TD011", CatalogCodes.Next(["TD001", "TD010", "TD003", "VT050", "TDX099"], "TD", 20));

    [Fact(DisplayName = "BR-CAT-01: tối thiểu 3 chữ số, quá 999 thì thêm chữ số")]
    public void Next_GrowsBeyondThreeDigits() =>
        Assert.Equal("TD1000", CatalogCodes.Next(["TD999"], "TD", 20));

    [Fact(DisplayName = "BR-CAT-01: mã có số kế tiếp dài quá cột thì bỏ qua, mã sinh ra luôn vừa 20 ký tự")]
    public void Next_SkipsCodesWhoseSuccessorWouldNotFit()
    {
        var next = CatalogCodes.Next(["TD005", "TD" + new string('9', 18)], "TD", 20);

        Assert.Equal("TD006", next);
    }

    [Fact(DisplayName = "BR-CAT-01: mã dài vừa đủ mà số kế tiếp vẫn vừa cột thì vẫn tính")]
    public void Next_KeepsLongCodeWhoseSuccessorFits() =>
        Assert.Equal("TD" + new string('9', 17) + "1", CatalogCodes.Next(["TD" + new string('9', 17) + "0"], "TD", 20));
}
