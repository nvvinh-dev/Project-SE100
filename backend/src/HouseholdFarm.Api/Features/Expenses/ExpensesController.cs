using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Expenses;

// Khoản chi không có PUT, DELETE: ghi nhầm thì điều chỉnh hoặc hủy, có lý do và lưu lịch sử (FR-EXP-02).
[ApiController]
[Route("api/expenses")]
[Authorize]
public sealed class ExpensesController(IExpenseService expenseService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ExpenseListItem>>> GetListAsync([FromQuery] ExpenseQuery query, CancellationToken ct) =>
        Ok(await expenseService.GetListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExpenseResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await expenseService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> CreateAsync(CreateExpenseRequest request, CancellationToken ct)
    {
        var expense = await expenseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = expense.Id }, expense);
    }

    [HttpPost("{id:guid}/correct")]
    public async Task<ActionResult<ExpenseResponse>> CorrectAsync(Guid id, CorrectExpenseRequest request, CancellationToken ct) =>
        Ok(await expenseService.CorrectAsync(id, request, ct));

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<ExpenseResponse>> VoidAsync(Guid id, VoidExpenseRequest request, CancellationToken ct) =>
        Ok(await expenseService.VoidAsync(id, request, ct));
}
