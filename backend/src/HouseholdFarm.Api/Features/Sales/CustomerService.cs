using System.Linq.Expressions;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Sales;

public sealed class CustomerService(AppDbContext db, ICurrentUser currentUser) : ICustomerService
{
    private static readonly Expression<Func<Customer, CustomerResponse>> _toResponse = c => new CustomerResponse(
        c.Id,
        c.Name,
        c.Phone,
        c.Note,
        c.Status,
        c.CreatedAt,
        c.Version);

    private static readonly Func<Customer, CustomerResponse> _toResponseCompiled = _toResponse.Compile();

    public async Task<PagedResult<CustomerListItem>> GetListAsync(CustomerQuery query, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        var customers = db.Customers.AsNoTracking();

        if (query.Status is { } status)
        {
            customers = customers.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = SearchPatterns.Contains(query.Search);
            // Số điện thoại lưu đã chuẩn hóa: người dùng gõ "+84 912" vẫn tìm được "0912...".
            var phonePattern = SearchPatterns.Contains(PhoneSearchText(query.Search));
            customers = customers.Where(c =>
                EF.Functions.ILike(c.Name, pattern, SearchPatterns.EscapeCharacter)
                || EF.Functions.ILike(c.Phone!, phonePattern, SearchPatterns.EscapeCharacter));
        }

        return await customers
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new CustomerListItem(c.Id, c.Name, c.Phone, c.Status))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<CustomerResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        return await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(_toResponse).SingleOrDefaultAsync(ct)
            ?? throw NotFound();
    }

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        // Không gán HouseholdId, CreatedAt, CreatedById: interceptor lấy từ người đang đăng nhập (BR-GEN-01).
        var customer = new Customer
        {
            Name = request.Name.Trim(),
            Phone = PhoneNumbers.Normalize(request.Phone),
            Note = TrimToNull(request.Note),
            Status = request.Status ?? RecordStatus.Active,
        };
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return _toResponseCompiled(customer);
    }

    public async Task<CustomerResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageFinance();

        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NotFound();

        // BR-GEN-11: người khác đã sửa sau lần đọc của người này thì lưu báo CONCURRENCY_CONFLICT, không ghi đè.
        db.Entry(customer).Property(c => c.Version).OriginalValue = request.Version;

        // BR-GEN-13: chuyển Inactive không chặn; giao dịch bán cũ vẫn giữ tham chiếu tới khách hàng.
        customer.Name = request.Name.Trim();
        customer.Phone = PhoneNumbers.Normalize(request.Phone);
        customer.Note = TrimToNull(request.Note);
        customer.Status = request.Status!.Value;
        await db.SaveChangesAsync(ct);

        return _toResponseCompiled(customer);
    }

    // Chuỗi tìm có thể chỉ là một phần số: bỏ khoảng trắng, dấu chấm, gạch và đổi tiền tố +84 thành 0 như khi lưu.
    private static string PhoneSearchText(string search)
    {
        var compact = string.Concat(search.Where(c => !char.IsWhiteSpace(c) && c is not '.' and not '-'));
        return compact.StartsWith("+84", StringComparison.Ordinal) ? "0" + compact[3..] : compact;
    }

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AppException NotFound() => AppException.NotFound("khách hàng");
}
