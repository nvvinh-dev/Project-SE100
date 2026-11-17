using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static HouseholdFarm.Api.Tests.Features.Sales.SaleTestData;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>Ghi, hủy thanh toán (FR-SAL-04, BR-SAL-06, BR-SAL-07).</summary>
public class PaymentTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string PaymentsPath = "/api/payments";

    private static object Pay(Guid saleId, decimal amount, Guid? requestId = null, DateOnly? paymentDate = null) => new
    {
        requestId = requestId ?? Guid.CreateVersion7(),
        saleId,
        amount,
        paymentDate = paymentDate ?? new DateOnly(2026, 10, 26),
        note = "Chuyển khoản",
    };

    [Fact(DisplayName = "FR-SAL-04: ghi thanh toán một phần, trạng thái PartiallyPaid, có nhật ký Payment.Create")]
    public async Task Create_PartialPayment_UpdatesStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct); // doanh thu 1.000.000
        var client = factory.CreateClientFor(household.MemberWithAllPermissions);

        // BR-SAL-06: ngày nhận tiền được trước ngày bán (tiền cọc).
        var response = await client.PostAsJsonAsync(PaymentsPath, Pay(sale.Id, 300_000m, paymentDate: new DateOnly(2026, 10, 1)), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var paymentId = (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid();
        var detail = (await (await client.GetAsync($"{SalesPath}/{sale.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(300_000m, detail.GetProperty("paidAmount").GetDecimal());
        Assert.Equal(700_000m, detail.GetProperty("outstandingAmount").GetDecimal());
        Assert.Equal("PartiallyPaid", detail.GetProperty("paymentStatus").GetString());
        Assert.Single(detail.GetProperty("payments").EnumerateArray());

        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(AuditActions.PaymentCreate, (await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == paymentId, ct)).Action);
    }

    [Fact(DisplayName = "TC-19: doanh thu 10.000.000, đã thu 6.000.000, ghi thêm 5.000.000 trả PAYMENT_EXCEEDS_OUTSTANDING")]
    public async Task TC19_CreatePayment_ExceedsOutstanding_ReturnsPaymentExceedsOutstanding()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct, quantity: 1000m, unitPrice: 10_000m);
        await CreatePaymentAsync(factory.Postgres, household, sale.Id, 6_000_000m, ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(PaymentsPath, Pay(sale.Id, 5_000_000m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.PaymentExceedsOutstanding, ct);
    }

    [Fact(DisplayName = "TC-17 (b): mùa vụ Closed còn công nợ 2.000.000, ghi thanh toán 2.000.000 thành công, trạng thái Paid")]
    public async Task TC17_CreatePayment_SeasonClosed_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.Closed, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct, quantity: 200m, unitPrice: 10_000m);
        var client = factory.CreateClientFor(household.Owner);

        var response = await client.PostAsJsonAsync(PaymentsPath, Pay(sale.Id, 2_000_000m), ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = (await (await client.GetAsync($"{SalesPath}/{sale.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Paid", detail.GetProperty("paymentStatus").GetString());
    }

    [Fact(DisplayName = "BR-SAL-06: hủy thanh toán làm công nợ tăng lại, trạng thái đổi đúng; hủy lần hai trả ALREADY_VOIDED")]
    public async Task Void_Payment_RestoresOutstanding()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.Closed, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        var payment = await CreatePaymentAsync(factory.Postgres, household, sale.Id, 1_000_000m, ct);
        var client = factory.CreateClientFor(household.Owner);

        var voided = await client.PostAsJsonAsync($"{PaymentsPath}/{payment.Id}/void", new { reason = "Ghi nhầm số tiền" }, ct);
        var again = await client.PostAsJsonAsync($"{PaymentsPath}/{payment.Id}/void", new { reason = "Lần hai" }, ct);

        Assert.Equal(HttpStatusCode.OK, voided.StatusCode);
        await again.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyVoided, ct);
        var detail = (await (await client.GetAsync($"{SalesPath}/{sale.Id}", ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(1_000_000m, detail.GetProperty("outstandingAmount").GetDecimal());
        Assert.Equal("Unpaid", detail.GetProperty("paymentStatus").GetString());
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Contains(
            AuditActions.PaymentVoid,
            await db.AuditLogs.IgnoreQueryFilters().Where(l => l.EntityId == payment.Id).Select(l => l.Action).ToListAsync(ct));
    }

    [Fact(DisplayName = "BR-SAL-06: ghi thanh toán cho giao dịch bán đã hủy trả ALREADY_VOIDED")]
    public async Task Create_VoidedSale_ReturnsAlreadyVoided()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct, voided: true);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(PaymentsPath, Pay(sale.Id, 1000m), ct);

        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AlreadyVoided, ct);
    }

    [Fact(DisplayName = "BR-GEN-10: gửi lại cùng requestId chỉ ghi một thanh toán")]
    public async Task Create_SameRequestIdTwice_RecordsOnce()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        var client = factory.CreateClientFor(household.Owner);
        var body = Pay(sale.Id, 600_000m, requestId: Guid.CreateVersion7());

        var first = await client.PostAsJsonAsync(PaymentsPath, body, ct);
        // Lần hai vượt công nợ còn lại nếu ghi thật; trả lại thanh toán cũ.
        var second = await client.PostAsJsonAsync(PaymentsPath, body, ct);

        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.Equal(
            (await first.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid(),
            (await second.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid());
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(1, await db.Payments.IgnoreQueryFilters().CountAsync(p => p.SaleId == sale.Id, ct));
    }

    [Fact(DisplayName = "BR-GEN-12: hai lần ghi thanh toán cùng lúc không vượt công nợ")]
    public async Task Create_ConcurrentPayments_DoNotExceedOutstanding()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct); // doanh thu 1.000.000

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            factory.CreateClientFor(household.Owner).PostAsJsonAsync(PaymentsPath, Pay(sale.Id, 800_000m), ct)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        await responses.Single(r => r.StatusCode != HttpStatusCode.Created)
            .AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.PaymentExceedsOutstanding, ct);
    }

    [Fact(DisplayName = "FR-SAL-04: Thành viên không có TC ghi thanh toán bị FORBIDDEN")]
    public async Task Create_WithoutFinance_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);

        var response = await factory.CreateClientFor(household.Member).PostAsJsonAsync(PaymentsPath, Pay(sale.Id, 1000m), ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }
}
