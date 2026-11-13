using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Sales;

// Giao dịch bán không có PUT, DELETE: ghi nhầm thì điều chỉnh hoặc hủy, có lý do và lưu lịch sử (FR-SAL-05).
[ApiController]
[Route("api/sales")]
[Authorize]
public sealed class SalesController(ISaleService saleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<SaleListItem>>> GetListAsync([FromQuery] SaleQuery query, CancellationToken ct) =>
        Ok(await saleService.GetListAsync(query, ct));

    [HttpGet("debts")]
    public async Task<ActionResult<SaleDebtsResponse>> GetDebtsAsync(CancellationToken ct) =>
        Ok(await saleService.GetDebtsAsync(ct));

    [HttpPost("{id:guid}/correct")]
    public async Task<ActionResult<SaleResponse>> CorrectAsync(Guid id, CorrectSaleRequest request, CancellationToken ct) =>
        Ok(await saleService.CorrectAsync(id, request, ct));

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<SaleResponse>> VoidAsync(Guid id, VoidSaleRequest request, CancellationToken ct) =>
        Ok(await saleService.VoidAsync(id, request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SaleResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await saleService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<SaleResponse>> CreateAsync(CreateSaleRequest request, CancellationToken ct)
    {
        var sale = await saleService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = sale.Id }, sale);
    }
}
