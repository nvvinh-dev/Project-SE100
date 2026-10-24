namespace HouseholdFarm.Api.Common.Responses;

/// <summary>
/// Cảnh báo của yêu cầu hiện tại (D-34). Service ghi cảnh báo vào đây và chỉ trả DTO;
/// <see cref="ResponseEnvelopeFilter"/> tự đưa cảnh báo vào phản hồi.
/// </summary>
/// <example>
/// <code>
/// _warnings.Add(WarningCodes.LowStock, $"Tồn {supply.Name} sau khi xuất còn {newStock} kg, thấp hơn ngưỡng 30 kg.");
/// </code>
/// </example>
public interface IWarningCollector
{
    IReadOnlyList<ResponseWarning> Warnings { get; }

    /// <param name="code">Hằng số trong <see cref="WarningCodes"/>.</param>
    /// <param name="message">Câu tiếng Việt, frontend hiển thị nguyên văn.</param>
    void Add(string code, string message);
}
