namespace HouseholdFarm.Api.Common.Errors;

/// <summary>
/// Lỗi nghiệp vụ: service ném, <see cref="GlobalExceptionHandler"/> chuyển thành ProblemDetails với mã HTTP theo mã lỗi.
/// Thao tác bị từ chối thì không có gì được lưu.
/// </summary>
/// <remarks>
/// <see cref="Exception.Message"/> được trả nguyên văn ở trường <c>detail</c> để frontend hiển thị,
/// nên viết tiếng Việt, nói rõ vì sao bị từ chối.
/// </remarks>
public sealed class AppException : Exception
{
    /// <param name="code">Hằng số trong <see cref="ErrorCodes"/>.</param>
    /// <param name="message">Câu tiếng Việt trả ở trường <c>detail</c>.</param>
    /// <param name="details">
    /// Dữ liệu kèm, trả ở trường <c>details</c>; với <c>CONFIRMATION_REQUIRED</c> luôn là <c>new { items }</c>,
    /// mỗi phần tử có <c>id</c>, <c>label</c> (BR-GEN-14).
    /// </param>
    public AppException(string code, string message, object? details = null)
        : this(code, message, details, errors: null)
    {
    }

    private AppException(string code, string message, object? details, IReadOnlyDictionary<string, string[]>? errors)
        : base(message)
    {
        // Mã gõ sai thì hỏng ngay khi chạy test, không để lọt ra client một mã frontend không biết.
        if (!ErrorCodes.IsDefined(code))
        {
            throw new ArgumentException($"Mã lỗi {code} không có trong ErrorCodes.", nameof(code));
        }

        Code = code;
        Details = details;
        Errors = errors;
    }

    public string Code { get; }

    public object? Details { get; }

    /// <summary>Tên trường camelCase → các thông báo lỗi; chỉ có với <c>VALIDATION_ERROR</c>.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    /// <summary>
    /// Bản ghi không tồn tại. BR-GEN-02: bản ghi thuộc hộ khác hoặc ngoài phạm vi làm việc cũng dùng lỗi này,
    /// để không tiết lộ là bản ghi có tồn tại.
    /// </summary>
    /// <param name="entityLabel">Tên bản ghi viết thường, ví dụ "vật tư", "mùa vụ".</param>
    public static AppException NotFound(string entityLabel) =>
        new(ErrorCodes.NotFound, $"Không tìm thấy {entityLabel}.");

    /// <param name="errors">Tên trường camelCase → các thông báo lỗi tiếng Việt.</param>
    public static AppException Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorCodes.ValidationError, "Dữ liệu gửi lên chưa hợp lệ, vui lòng kiểm tra lại.", details: null, errors);
}
