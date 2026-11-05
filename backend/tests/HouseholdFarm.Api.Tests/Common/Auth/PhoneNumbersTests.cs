using HouseholdFarm.Api.Common.Auth;

namespace HouseholdFarm.Api.Tests.Common.Auth;

public class PhoneNumbersTests
{
    [Theory(DisplayName = "BR-USR-01: bỏ khoảng trắng, dấu chấm, dấu gạch; đổi +84, 84 thành 0")]
    [InlineData("0912345678", "0912345678")]
    [InlineData(" 0912 345 678 ", "0912345678")]
    [InlineData("0912.345.678", "0912345678")]
    [InlineData("0912-345-678", "0912345678")]
    [InlineData("+84912345678", "0912345678")]
    [InlineData("+84 912 345 678", "0912345678")]
    [InlineData("84912345678", "0912345678")]
    public void Normalize_ValidInput_ReturnsTenDigitsStartingWithZero(string input, string expected) =>
        Assert.Equal(expected, PhoneNumbers.Normalize(input));

    [Theory(DisplayName = "BR-USR-01: không ra được 10 chữ số bắt đầu bằng 0 thì không hợp lệ")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("091234567")]
    [InlineData("09123456789")]
    [InlineData("1912345678")]
    [InlineData("8491234567")]
    [InlineData("+8491234567")]
    [InlineData("0912a45678")]
    [InlineData("(091) 234 5678")]
    [InlineData("０９１２３４５６７８")]
    public void Normalize_InvalidInput_ReturnsNull(string? input) => Assert.Null(PhoneNumbers.Normalize(input));
}
