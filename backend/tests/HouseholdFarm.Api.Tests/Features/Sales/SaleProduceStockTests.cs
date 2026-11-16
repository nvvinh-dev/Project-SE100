using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using static HouseholdFarm.Api.Tests.Features.Sales.SaleTestData;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>
/// Không bán vượt tồn nông sản (FR-SAL-02), chạy với bản thật của <c>IProduceStockService</c>.
/// </summary>
public class SaleProduceStockTests(ApiFactory factory) : IClassFixture<ApiFactory>
{

    [Fact(DisplayName = "TC-11: đã thu 1.000 kg loại 1, đã bán 700 kg, bán thêm 400 kg trả INSUFFICIENT_PRODUCE_STOCK")]
    public async Task TC11_CreateSale_ExceedsRemainingStock_ReturnsInsufficientProduceStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 1000m, ct);
        await CreateSaleAsync(factory.Postgres, household, season.Id, ct, quantity: 700m);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(SalesPath, Create(season.Id, quantity: 400m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientProduceStock, ct);
    }

    [Fact(DisplayName = "TC-12: đã thu 500 kg loại 1 và 300 kg loại 2, bán 600 kg loại 1 trả INSUFFICIENT_PRODUCE_STOCK")]
    public async Task TC12_CreateSale_OtherGradeNotCombined_ReturnsInsufficientProduceStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 500m, ct);
        await CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct, QualityGrade.Grade2);

        var response = await factory.CreateClientFor(household.Owner)
            .PostAsJsonAsync(SalesPath, Create(season.Id, quantity: 600m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.InsufficientProduceStock, ct);
    }

    [Fact(DisplayName = "FR-SAL-02: bán đúng bằng tồn còn lại thành công (bản thật)")]
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
}
