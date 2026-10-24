namespace HouseholdFarm.Api.Common.Errors;

/// <summary>
/// Mã lỗi trả về ở trường <c>code</c> của ProblemDetails, đúng danh mục mã lỗi của dự án.
/// Thêm mã mới thì bổ sung danh mục trước, rồi thêm hằng số kèm mã HTTP và tiêu đề vào bảng bên dưới.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string Forbidden = "FORBIDDEN";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string TooManyAttempts = "TOO_MANY_ATTEMPTS";
    public const string Unauthenticated = "UNAUTHENTICATED";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
    public const string PhoneAlreadyUsed = "PHONE_ALREADY_USED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string ConfirmationRequired = "CONFIRMATION_REQUIRED";
    public const string AlreadyVoided = "ALREADY_VOIDED";
    public const string CatalogInactive = "CATALOG_INACTIVE";
    public const string CatalogInUse = "CATALOG_IN_USE";
    public const string CodeAlreadyUsed = "CODE_ALREADY_USED";
    public const string NameAlreadyUsed = "NAME_ALREADY_USED";
    public const string UnitLocked = "UNIT_LOCKED";
    public const string SeasonInvalidState = "SEASON_INVALID_STATE";
    public const string SeasonOverlap = "SEASON_OVERLAP";
    public const string SeasonPlotBusy = "SEASON_PLOT_BUSY";
    public const string SeasonProduceRemaining = "SEASON_PRODUCE_REMAINING";
    public const string SeasonHasHarvest = "SEASON_HAS_HARVEST";
    public const string TaskInvalidState = "TASK_INVALID_STATE";
    public const string AssigneeNotInSeason = "ASSIGNEE_NOT_IN_SEASON";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string StockTxNotLast = "STOCK_TX_NOT_LAST";
    public const string StockTxManagedByTask = "STOCK_TX_MANAGED_BY_TASK";
    public const string InsufficientProduceStock = "INSUFFICIENT_PRODUCE_STOCK";
    public const string ProduceStockNegative = "PRODUCE_STOCK_NEGATIVE";
    public const string SaleHasPayments = "SALE_HAS_PAYMENTS";
    public const string RevenueBelowPaid = "REVENUE_BELOW_PAID";
    public const string PaymentExceedsOutstanding = "PAYMENT_EXCEEDS_OUTSTANDING";
    public const string AIQuotaExceeded = "AI_QUOTA_EXCEEDED";
    public const string AIUnavailable = "AI_UNAVAILABLE";
    public const string InternalError = "INTERNAL_ERROR";

    // 409 là xung đột với dữ liệu của người khác hoặc bản ghi khác; 422 là vi phạm quy tắc nghiệp vụ.
    private static readonly Dictionary<string, (int Status, string Title)> _definitions = new()
    {
        [ValidationError] = (StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ"),
        [Unauthenticated] = (StatusCodes.Status401Unauthorized, "Chưa đăng nhập"),
        [InvalidCredentials] = (StatusCodes.Status401Unauthorized, "Sai thông tin đăng nhập"),
        [InvalidRefreshToken] = (StatusCodes.Status401Unauthorized, "Phiên đăng nhập không hợp lệ"),
        [Forbidden] = (StatusCodes.Status403Forbidden, "Không có quyền"),
        [AccountLocked] = (StatusCodes.Status403Forbidden, "Tài khoản bị khóa"),
        [NotFound] = (StatusCodes.Status404NotFound, "Không tìm thấy"),
        [ConcurrencyConflict] = (StatusCodes.Status409Conflict, "Bản ghi đã bị người khác sửa"),
        [PhoneAlreadyUsed] = (StatusCodes.Status409Conflict, "Số điện thoại đã được dùng"),
        [CodeAlreadyUsed] = (StatusCodes.Status409Conflict, "Mã bị trùng"),
        [NameAlreadyUsed] = (StatusCodes.Status409Conflict, "Tên bị trùng"),
        [ConfirmationRequired] = (StatusCodes.Status422UnprocessableEntity, "Cần xác nhận"),
        [AlreadyVoided] = (StatusCodes.Status422UnprocessableEntity, "Bản ghi đã bị hủy"),
        [CatalogInactive] = (StatusCodes.Status422UnprocessableEntity, "Danh mục đang ngừng sử dụng"),
        [CatalogInUse] = (StatusCodes.Status422UnprocessableEntity, "Danh mục đang được mùa vụ sử dụng"),
        [UnitLocked] = (StatusCodes.Status422UnprocessableEntity, "Không đổi được đơn vị"),
        [SeasonInvalidState] = (StatusCodes.Status422UnprocessableEntity, "Trạng thái mùa vụ không cho phép"),
        [SeasonOverlap] = (StatusCodes.Status422UnprocessableEntity, "Mùa vụ chồng lấn"),
        [SeasonPlotBusy] = (StatusCodes.Status422UnprocessableEntity, "Thửa đang có mùa vụ canh tác"),
        [SeasonProduceRemaining] = (StatusCodes.Status422UnprocessableEntity, "Còn tồn nông sản"),
        [SeasonHasHarvest] = (StatusCodes.Status422UnprocessableEntity, "Mùa vụ đã có thu hoạch hoặc bán"),
        [TaskInvalidState] = (StatusCodes.Status422UnprocessableEntity, "Trạng thái công việc không cho phép"),
        [AssigneeNotInSeason] = (StatusCodes.Status422UnprocessableEntity, "Người thực hiện không thuộc mùa vụ"),
        [InsufficientStock] = (StatusCodes.Status422UnprocessableEntity, "Không đủ tồn kho"),
        [StockTxNotLast] = (StatusCodes.Status422UnprocessableEntity, "Đã có thao tác sau giao dịch kho"),
        [StockTxManagedByTask] = (StatusCodes.Status422UnprocessableEntity, "Giao dịch xuất phải sửa qua công việc"),
        [InsufficientProduceStock] = (StatusCodes.Status422UnprocessableEntity, "Không đủ tồn nông sản"),
        [ProduceStockNegative] = (StatusCodes.Status422UnprocessableEntity, "Tồn nông sản bị âm"),
        [SaleHasPayments] = (StatusCodes.Status422UnprocessableEntity, "Giao dịch bán còn thanh toán"),
        [RevenueBelowPaid] = (StatusCodes.Status422UnprocessableEntity, "Doanh thu nhỏ hơn số đã thu"),
        [PaymentExceedsOutstanding] = (StatusCodes.Status422UnprocessableEntity, "Thanh toán vượt công nợ"),
        [TooManyAttempts] = (StatusCodes.Status429TooManyRequests, "Đăng nhập sai quá nhiều lần"),
        [AIQuotaExceeded] = (StatusCodes.Status429TooManyRequests, "Hết lượt AI trong ngày"),
        [InternalError] = (StatusCodes.Status500InternalServerError, "Lỗi hệ thống"),
        [AIUnavailable] = (StatusCodes.Status503ServiceUnavailable, "Dịch vụ AI không phản hồi"),
    };

    /// <summary>Tất cả mã lỗi đã khai báo.</summary>
    public static IReadOnlyCollection<string> All => _definitions.Keys;

    public static bool IsDefined(string code) => _definitions.ContainsKey(code);

    /// <summary>Mã HTTP trả về cho mã lỗi.</summary>
    public static int StatusOf(string code) => _definitions[code].Status;

    /// <summary>Tiêu đề ngắn tiếng Việt, trả ở trường <c>title</c> của ProblemDetails.</summary>
    public static string TitleOf(string code) => _definitions[code].Title;
}
