using HouseholdFarm.Api.Common.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseholdFarm.Api.Features.Sales;

// Không có API xóa khách hàng: ngừng giao dịch thì chuyển Inactive (FR-SAL-08, BR-GEN-13).
[ApiController]
[Route("api/customers")]
[Authorize]
public sealed class CustomersController(ICustomerService customerService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<CustomerListItem>>> GetListAsync([FromQuery] CustomerQuery query, CancellationToken ct) =>
        Ok(await customerService.GetListAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetAsync(Guid id, CancellationToken ct) =>
        Ok(await customerService.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = await customerService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetAsync), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct) =>
        Ok(await customerService.UpdateAsync(id, request, ct));
}
