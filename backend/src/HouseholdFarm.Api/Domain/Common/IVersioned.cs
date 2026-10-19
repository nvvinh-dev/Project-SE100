namespace HouseholdFarm.Api.Domain.Common;

// Bản ghi sửa được. Bảng có cột updated_at luôn có concurrency token Version (ánh xạ cột hệ thống xmin),
// nên gom chung một interface. Người lưu sau khi người khác đã sửa sẽ bị từ chối thay vì ghi đè (BR-GEN-11).
public interface IVersioned
{
    DateTime? UpdatedAt { get; set; }

    Guid? UpdatedById { get; set; }

    uint Version { get; set; }
}
