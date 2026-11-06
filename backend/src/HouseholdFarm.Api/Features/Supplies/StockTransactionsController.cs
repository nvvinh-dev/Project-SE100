using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Supplies;

// Giao dịch kho không có PUT, DELETE: ghi nhầm thì hủy rồi tạo lại.
[ApiController]
[Route("api/stock-transactions")]
[Authorize]
public sealed class StockTransactionsController(IStockTransactionService stockTransactionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<StockTransactionListItem>>> GetListAsync(
        [FromQuery] StockTransactionQuery query,
        CancellationToken ct) =>
        Ok(await stockTransactionService.GetListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockTransactionResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await stockTransactionService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<StockTransactionResponse>> CreateAsync(
        CreateStockTransactionRequest request,
        CancellationToken ct)
    {
        var transaction = await stockTransactionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = transaction.Id }, transaction);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<StockTransactionResponse>> VoidAsync(
        Guid id,
        VoidStockTransactionRequest request,
        CancellationToken ct) =>
        Ok(await stockTransactionService.VoidAsync(id, request, ct));
}
