using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static HouseholdFarm.Api.Tests.Features.Sales.SaleTestData;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>Điều chỉnh, hủy giao dịch bán (FR-SAL-05, BR-SAL-03, BR-SAL-04; liên kết phiếu thu FR-SAL-03).</summary>
public class SaleCorrectVoidTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static object Correct(
        uint version,
        decimal quantity = 100m,
        decimal unitPrice = 10_000m,
        string grade = "Grade1",
        string? channel = "Trader",
        Guid? customerId = null,
        DateOnly? saleDate = null,
        IReadOnlyList<Guid>? harvestRecordIds = null) => new
    {
        customerId,
        channel,
        qualityGrade = grade,
        quantity,
        unitPrice,
        saleDate = saleDate ?? new DateOnly(2026, 10, 25),
        harvestRecordIds,
        reason = "Ghi nhầm",
        version,
    };

    private async Task<(HttpClient Client, Sale Sale, uint Version, Guid SeasonId, TestHousehold Household)> ArrangeAsync(
        decimal harvested,
        CancellationToken ct,
        decimal soldQuantity = 100m)
    {
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, harvested, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct, quantity: soldQuantity);
        var client = factory.CreateClientFor(household.Owner);
        var version = await VersionAsync(client, sale.Id, ct);
        return (client, sale, version, season.Id, household);
    }

    private static async Task<uint> VersionAsync(HttpClient client, Guid saleId, CancellationToken ct) =>
        (await (await client.GetAsync($"{SalesPath}/{saleId}", ct)).ReadJsonAsync(ct))
            .GetProperty("data").GetProperty("version").GetUInt32();

    [Fact(DisplayName = "BR-SAL-03: tăng số lượng trong giới hạn tồn (đã hoàn lại lượng của chính giao dịch) thành công, nhật ký chỉ trường đổi")]
    public async Task Correct_IncreaseWithinStock_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, sale, version, _, _) = await ArrangeAsync(harvested: 300m, ct);

        var response = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/correct", Correct(version, quantity: 300m), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(300m, (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("quantity").GetDecimal());
        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == sale.Id && l.Action == AuditActions.SaleCorrect, ct);
        Assert.Equal("Ghi nhầm", log.Reason);
        using var newValues = JsonDocument.Parse(log.NewValues!);
        Assert.Equal(["quantity"], newValues.RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact(DisplayName = "BR-SAL-03: tăng số lượng vượt tồn trả INSUFFICIENT_PRODUCE_STOCK")]
    public async Task Correct_IncreaseBeyondStock_ReturnsInsufficientProduceStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, sale, version, _, _) = await ArrangeAsync(harvested: 300m, ct);

        var response = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/correct", Correct(version, quantity: 300.5m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientProduceStock, ct);
    }

    [Fact(DisplayName = "BR-SAL-03: giảm đơn giá làm doanh thu dưới số đã thu trả REVENUE_BELOW_PAID")]
    public async Task Correct_RevenueBelowPaid_ReturnsRevenueBelowPaid()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, sale, version, _, household) = await ArrangeAsync(harvested: 300m, ct);
        await CreatePaymentAsync(factory.Postgres, household, sale.Id, 800_000m, ct);

        var response = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/correct", Correct(version, unitPrice: 7_000m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.RevenueBelowPaid, ct);
    }

    [Theory(DisplayName = "BR-SAL-03: bỏ cả khách hàng lẫn kênh bán, đổi ngày bán về trước ngày bắt đầu mùa vụ trả VALIDATION_ERROR")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Correct_InvalidData_ReturnsValidationError(bool beforeSeasonStart)
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, sale, version, _, _) = await ArrangeAsync(harvested: 300m, ct);
        var body = beforeSeasonStart
            ? Correct(version, saleDate: new DateOnly(2026, 9, 30))
            : Correct(version, channel: null);

        var response = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/correct", body, ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "BR-SAL-03, BR-GEN-13: đổi sang khách hàng Inactive trả CATALOG_INACTIVE")]
    public async Task Correct_ToInactiveCustomer_ReturnsCatalogInactive()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, sale, version, _, household) = await ArrangeAsync(harvested: 300m, ct);
        var inactive = await CreateCustomerAsync(factory.Postgres, household, ct, "Anh Tư", RecordStatus.Inactive);

        var response = await client.PostAsJsonAsync(
            $"{SalesPath}/{sale.Id}/correct", Correct(version, customerId: inactive.Id), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInactive, ct);
    }

    [Fact(DisplayName = "BR-SAL-03: đổi loại chất lượng gỡ liên kết phiếu thu; nhật ký ghi danh sách cũ và mới")]
    public async Task Correct_ChangeGrade_RemovesHarvestLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var grade1 = await CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct, QualityGrade.Grade2);
        var client = factory.CreateClientFor(household.Owner);
        var created = (await (await client.PostAsJsonAsync(
            SalesPath, Create(season.Id, quantity: 100m, harvestRecordIds: [grade1.Id]), ct)).ReadJsonAsync(ct)).GetProperty("data");
        var saleId = created.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync(
            $"{SalesPath}/{saleId}/correct", Correct(created.GetProperty("version").GetUInt32(), grade: "Grade2"), ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("harvestLinks").EnumerateArray());
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.SaleHarvestLinks.IgnoreQueryFilters().AnyAsync(l => l.SaleId == saleId, ct));
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == saleId && l.Action == AuditActions.SaleCorrect, ct);
        Assert.Contains("harvestRecordIds", log.OldValues);
    }

    [Fact(DisplayName = "FR-SAL-03: danh sách phiếu thu gửi lên khi điều chỉnh thay toàn bộ liên kết cũ")]
    public async Task Correct_ReplaceHarvestLinks_ReplacesAll()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var first = await CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct);
        var second = await CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct);
        var client = factory.CreateClientFor(household.Owner);
        var created = (await (await client.PostAsJsonAsync(
            SalesPath, Create(season.Id, quantity: 100m, harvestRecordIds: [first.Id]), ct)).ReadJsonAsync(ct)).GetProperty("data");
        var saleId = created.GetProperty("id").GetGuid();

        var response = await client.PostAsJsonAsync(
            $"{SalesPath}/{saleId}/correct",
            Correct(created.GetProperty("version").GetUInt32(), unitPrice: 12_000m, harvestRecordIds: [second.Id]),
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var links = (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("harvestLinks").EnumerateArray()
            .Select(l => l.GetProperty("harvestRecordId").GetGuid());
        Assert.Equal([second.Id], links);
    }

    [Fact(DisplayName = "BR-GEN-11: gửi version cũ trả CONCURRENCY_CONFLICT")]
    public async Task Correct_StaleVersion_ReturnsConcurrencyConflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, sale, version, _, _) = await ArrangeAsync(harvested: 300m, ct);
        await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/correct", Correct(version, quantity: 150m), ct);

        var response = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/correct", Correct(version, quantity: 160m), ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);
    }

    [Fact(DisplayName = "Mục 4.3: điều chỉnh giao dịch bán khi mùa vụ đã chốt trả SEASON_INVALID_STATE")]
    public async Task Correct_SeasonClosed_ReturnsSeasonInvalidState()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.Closed, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        var client = factory.CreateClientFor(household.Owner);

        var response = await client.PostAsJsonAsync(
            $"{SalesPath}/{sale.Id}/correct", Correct(await VersionAsync(client, sale.Id, ct)), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "BR-SAL-04: hủy khi còn thanh toán chưa hủy trả SALE_HAS_PAYMENTS; hủy hết thanh toán rồi hủy được")]
    public async Task Void_WithPayments_RejectedThenSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (client, sale, _, _, household) = await ArrangeAsync(harvested: 300m, ct);
        var payment = await CreatePaymentAsync(factory.Postgres, household, sale.Id, 100_000m, ct);

        var rejected = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/void", new { reason = "Khách trả hàng" }, ct);
        await client.PostAsJsonAsync($"/api/payments/{payment.Id}/void", new { reason = "Hoàn tiền" }, ct);
        var voided = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/void", new { reason = "Khách trả hàng" }, ct);
        var again = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/void", new { reason = "Lần hai" }, ct);

        await rejected.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SaleHasPayments, ct);
        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        Assert.Equal("Khách trả hàng", (await voided.ReadJsonAsync(ct)).GetProperty("data").GetProperty("voidReason").GetString());
        await again.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyVoided, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.True(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == sale.Id && l.Action == AuditActions.SaleVoid, ct));
    }

    [Fact(DisplayName = "FR-SAL-05: Thành viên không có TC điều chỉnh, hủy giao dịch bán bị FORBIDDEN")]
    public async Task CorrectVoid_WithoutFinance_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, sale, version, _, household) = await ArrangeAsync(harvested: 300m, ct);
        var client = factory.CreateClientFor(household.Member);

        var correct = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/correct", Correct(version), ct);
        var voided = await client.PostAsJsonAsync($"{SalesPath}/{sale.Id}/void", new { reason = "Ghi nhầm" }, ct);

        await correct.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await voided.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }
}
