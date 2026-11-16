using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static HouseholdFarm.Api.Tests.Features.Sales.SaleTestData;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>Tạo giao dịch bán (FR-SAL-01, FR-SAL-02, FR-SAL-03, FR-SAL-06; BR-SAL-01, BR-SAL-05, BR-GEN-10).</summary>
public class SaleCreateTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "FR-SAL-01: tạo giao dịch bán thành công, doanh thu = số lượng × đơn giá, có nhật ký Sale.Create")]
    public async Task Create_ValidSale_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 500m, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(SalesPath, Create(season.Id, quantity: 120.5m, unitPrice: 12_345.6789m), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        var id = data.GetProperty("id").GetGuid();
        Assert.Equal($"{SalesPath}/{id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("TD001", data.GetProperty("plotCode").GetString());
        Assert.Equal("Kilogram", data.GetProperty("yieldUnit").GetString());
        Assert.Equal("Trader", data.GetProperty("channel").GetString());
        // 120,5 × 12.345,6789 = 1.487.654,30745 → làm tròn 2 chữ số.
        Assert.Equal(1_487_654.31m, data.GetProperty("revenue").GetDecimal());
        Assert.Equal(0m, data.GetProperty("paidAmount").GetDecimal());
        Assert.Equal(1_487_654.31m, data.GetProperty("outstandingAmount").GetDecimal());
        Assert.Equal("Unpaid", data.GetProperty("paymentStatus").GetString());

        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == id, ct);
        Assert.Equal(AuditActions.SaleCreate, log.Action);
    }

    [Fact(DisplayName = "FR-SAL-02: bán đúng bằng tồn còn lại thành công")]
    public async Task Create_QuantityEqualsStock_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 1000m, ct);
        await CreateSaleAsync(factory.Postgres, household, season.Id, ct, quantity: 700m);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(SalesPath, Create(season.Id, quantity: 300m), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact(DisplayName = "FR-SAL-02: số lượng vượt tồn trả INSUFFICIENT_PRODUCE_STOCK, không tạo giao dịch")]
    public async Task Create_ExceedsStock_ReturnsInsufficientProduceStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 100m, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(SalesPath, Create(season.Id, quantity: 100.001m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientProduceStock, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.Sales.IgnoreQueryFilters().AnyAsync(s => s.SeasonId == season.Id, ct));
    }

    [Fact(DisplayName = "BR-SAL-01: thiếu cả khách hàng và kênh bán trả VALIDATION_ERROR")]
    public async Task Create_NoCustomerNoChannel_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(SalesPath, Create(season.Id, channel: null), ct);

        var body = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.True(body.GetProperty("errors").TryGetProperty("channel", out _));
    }

    [Theory(DisplayName = "BR-SAL-01: số lượng ≤ 0, đơn giá âm, ngày bán sau hôm nay hoặc trước ngày bắt đầu mùa vụ trả VALIDATION_ERROR")]
    [InlineData("quantity")]
    [InlineData("unitPrice")]
    [InlineData("futureDate")]
    [InlineData("beforeSeasonStart")]
    public async Task Create_InvalidData_ReturnsValidationError(string invalid)
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 500m, ct);
        var farFuture = DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime).AddDays(2);
        var body = invalid switch
        {
            "quantity" => Create(season.Id, quantity: 0m),
            "unitPrice" => Create(season.Id, unitPrice: -1m),
            "futureDate" => Create(season.Id, saleDate: farFuture),
            // Mùa vụ bắt đầu 2026-10-01.
            _ => Create(season.Id, saleDate: new DateOnly(2026, 9, 30)),
        };

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(SalesPath, body, ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "BR-SAL-01: giá 0 vẫn ghi được, trạng thái thanh toán là Đã thanh toán")]
    public async Task Create_ZeroPrice_IsPaid()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 500m, ct);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(SalesPath, Create(season.Id, unitPrice: 0m), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Paid", (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("paymentStatus").GetString());
    }

    [Fact(DisplayName = "BR-SAL-01, BR-GEN-13: khách hàng Inactive trả CATALOG_INACTIVE; khách hàng Active thì có tên trong phản hồi")]
    public async Task Create_WithCustomer_ChecksStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 500m, ct);
        var active = await CreateCustomerAsync(factory.Postgres, household, ct, "Chị Lan");
        var inactive = await CreateCustomerAsync(factory.Postgres, household, ct, "Anh Tư", RecordStatus.Inactive);
        var client = factory.CreateClientFor(household.Owner);

        var rejected = await client.PostAsJsonAsync(SalesPath, Create(season.Id, channel: null, customerId: inactive.Id), ct);
        var accepted = await client.PostAsJsonAsync(SalesPath, Create(season.Id, channel: null, customerId: active.Id), ct);

        await rejected.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInactive, ct);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal("Chị Lan", (await accepted.ReadJsonAsync(ct)).GetProperty("data").GetProperty("customerName").GetString());
    }

    [Theory(DisplayName = "Mục 4.3: tạo giao dịch bán cho mùa vụ không InProgress trả SEASON_INVALID_STATE")]
    [InlineData(SeasonStatus.Planned)]
    [InlineData(SeasonStatus.Closed)]
    [InlineData(SeasonStatus.Cancelled)]
    public async Task Create_SeasonNotInProgress_ReturnsSeasonInvalidState(SeasonStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, status, ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(SalesPath, Create(season.Id), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "TC-21: Thành viên không có TC tạo giao dịch bán bị FORBIDDEN; Thành viên có TC ngoài phân công tạo được")]
    public async Task TC21_CreateSale_MemberWithoutAndWithFinance_ForbiddenThenSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 500m, ct);

        var forbidden = await factory.CreateClientFor(household.Member).PostAsJsonAsync(SalesPath, Create(season.Id), ct);
        var allowed = await factory.CreateClientFor(household.MemberWithAllPermissions)
            .PostAsJsonAsync(SalesPath, Create(season.Id), ct);

        await forbidden.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    [Fact(DisplayName = "TC-14: gửi hai lần cùng requestId chỉ tạo một giao dịch, lần hai trả đúng giao dịch đó")]
    public async Task TC14_CreateSale_SameRequestIdTwice_ReturnsSameSale()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 150m, ct);
        var client = factory.CreateClientFor(household.Owner);
        var body = Create(season.Id, quantity: 100m, requestId: Guid.CreateVersion7());

        var first = await client.PostAsJsonAsync(SalesPath, body, ct);
        // Lần hai vẫn thành công dù tồn chỉ còn 50: trả lại giao dịch cũ, không kiểm tra tồn lại.
        var second = await client.PostAsJsonAsync(SalesPath, body, ct);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(
            (await first.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid(),
            (await second.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid());
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(1, await db.Sales.IgnoreQueryFilters().CountAsync(s => s.SeasonId == season.Id, ct));
    }

    [Fact(DisplayName = "BR-GEN-10: hai yêu cầu cùng requestId gửi đồng thời vẫn chỉ tạo một giao dịch")]
    public async Task Create_SameRequestIdConcurrently_CreatesOneSale()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 500m, ct);
        var body = Create(season.Id, requestId: Guid.CreateVersion7());

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            factory.CreateClientFor(household.Owner).PostAsJsonAsync(SalesPath, body, ct)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var ids = new HashSet<Guid>();
        foreach (var r in responses)
        {
            ids.Add((await r.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid());
        }

        Assert.Single(ids);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(1, await db.Sales.IgnoreQueryFilters().CountAsync(s => s.SeasonId == season.Id, ct));
    }

    [Fact(DisplayName = "FR-SAL-03: liên kết phiếu thu cùng mùa vụ, cùng loại thành công, phản hồi có ngày thu và sản lượng")]
    public async Task Create_WithValidHarvestLinks_SavesLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var first = await CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct);
        var second = await CreateHarvestAsync(factory.Postgres, household, season.Id, 200m, ct);

        // BR-SAL-05: không ràng buộc số lượng giữa phiếu và lượng bán.
        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            SalesPath, Create(season.Id, quantity: 50m, harvestRecordIds: [first.Id, second.Id, first.Id]), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var links = (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("harvestLinks").EnumerateArray().ToList();
        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.GetProperty("harvestRecordId").GetGuid() == first.Id
            && l.GetProperty("quantity").GetDecimal() == 300m
            && l.GetProperty("harvestDate").GetString() == "2026-10-20");
    }

    [Theory(DisplayName = "BR-SAL-05: liên kết phiếu khác loại hoặc đã Void trả VALIDATION_ERROR")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Create_WithInvalidHarvestLink_ReturnsValidationError(bool voided)
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct);
        var invalid = await CreateHarvestAsync(
            factory.Postgres, household, season.Id, 200m, ct, grade: voided ? QualityGrade.Grade1 : QualityGrade.Grade2, voided);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(SalesPath, Create(season.Id, quantity: 50m, harvestRecordIds: [invalid.Id]), ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.Sales.IgnoreQueryFilters().AnyAsync(s => s.SeasonId == season.Id, ct));
    }
}
