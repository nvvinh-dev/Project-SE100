using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Sales;

// Thanh toán chỉ có ghi và hủy; ghi nhầm thì hủy rồi ghi lại (BR-SAL-07). Xem thanh toán qua chi tiết giao dịch bán.
[ApiController]
[Route("api/payments")]
[Authorize]
public sealed class PaymentsController(IPaymentService paymentService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> CreateAsync(CreatePaymentRequest request, CancellationToken ct)
    {
        var payment = await paymentService.CreateAsync(request, ct);
        // Không có API đọc riêng một thanh toán: thanh toán nằm trong chi tiết giao dịch bán.
        return Created($"/api/sales/{payment.SaleId}", payment);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<PaymentResponse>> VoidAsync(Guid id, VoidPaymentRequest request, CancellationToken ct) =>
        Ok(await paymentService.VoidAsync(id, request, ct));
}
