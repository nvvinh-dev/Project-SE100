namespace HouseholdFarm.Api.Domain.Common;

// Bản ghi giao dịch tạo kèm request_id do frontend sinh; duy nhất trong hộ để yêu cầu gửi lại không tạo bản ghi thứ hai (BR-GEN-10).
public interface IRequestIdentified
{
    Guid Id { get; }

    Guid RequestId { get; set; }
}
