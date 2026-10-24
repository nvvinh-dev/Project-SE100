using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Common.Probe;

// Chỉ có trong project test, để thử phần dùng chung (bọc phản hồi, lỗi, kiểm tra dữ liệu, JSON) qua HTTP thật
// mà không phụ thuộc module nghiệp vụ nào.
[ApiController]
[Route("api/probe")]
public sealed class ProbeController(IWarningCollector warnings) : ControllerBase
{
    public const int SampleTotalCount = 57;

    public const string UnexpectedMessage = "Chuỗi kết nối nội bộ không được lộ ra ngoài";

    public static readonly ProbeResponse Sample = new(
        Guid.CreateVersion7(),
        "Thửa sau nhà",
        1500.5m,
        SoilType.Loam,
        new DateOnly(2026, 10, 1),
        new DateTime(2026, 10, 1, 7, 30, 0, DateTimeKind.Utc),
        83921);

    [HttpGet("items/{id:guid}")]
    public ActionResult<ProbeResponse> Get(Guid id) => Sample with { Id = id };

    [HttpGet("items")]
    public ActionResult<PagedResult<ProbeListItem>> List([FromQuery] ProbeQuery query)
    {
        var items = Enumerable.Range((query.Page - 1) * query.PageSize + 1, query.PageSize)
            .Select(n => new ProbeListItem(n))
            .ToList();
        return Ok(new PagedResult<ProbeListItem>(items, query.Page, query.PageSize, SampleTotalCount));
    }

    [HttpPost("items")]
    public ActionResult<ProbeResponse> Create(CreateProbeRequest request) => CreatedAtAction(
        nameof(Get),
        new { id = Sample.Id },
        Sample with { Name = request.Name, AreaM2 = request.AreaM2, SoilType = request.SoilType, StartDate = request.StartDate });

    [HttpDelete("items/{id:guid}")]
    public IActionResult Delete(Guid id) => NoContent();

    [HttpGet("warnings")]
    public ActionResult<ProbeResponse> GetWithWarnings()
    {
        warnings.Add(WarningCodes.LowStock, "Tồn Phân NPK sau khi xuất còn 20 kg, thấp hơn ngưỡng 30 kg.");
        warnings.Add(WarningCodes.NegativeStock, "Tồn Phân NPK sau khi xuất còn -3 kg.");
        return Sample;
    }

    [HttpGet("errors/insufficient-stock")]
    public IActionResult ThrowInsufficientStock() => throw new AppException(
        ErrorCodes.InsufficientStock,
        "Phân NPK chỉ còn 5 kg, không đủ để xuất 8 kg.",
        new { supplyId = Sample.Id, available = 5, requested = 8, unit = Unit.Kilogram });

    [HttpGet("errors/confirmation-required")]
    public IActionResult ThrowConfirmationRequired() => throw new AppException(
        ErrorCodes.ConfirmationRequired,
        "Các công việc chưa làm dưới đây sẽ bị hủy khi chốt mùa vụ.",
        new { items = new[] { new { id = Sample.Id, label = "Tưới nước - 28/09/2026" } } });

    [HttpGet("errors/not-found")]
    public IActionResult ThrowNotFound() => throw AppException.NotFound("vật tư");

    [HttpGet("errors/concurrency")]
    public IActionResult ThrowConcurrency() => throw new DbUpdateConcurrencyException("Bản ghi đã đổi version.");

    [HttpGet("errors/unexpected")]
    public IActionResult ThrowUnexpected() => throw new InvalidOperationException(UnexpectedMessage);
}
