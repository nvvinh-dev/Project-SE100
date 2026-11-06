import type { PageQuery } from "@/lib/api-client";
import type { RecordStatus, SupplyGroup, Unit } from "@/lib/enums";

// Trùng tên DTO backend (Features/Supplies/SupplyDtos.cs).

/**
 * Chi tiết một vật tư. Thiếu quyền Quản lý kho thì API không trả hai trường giá; người có quyền cũng không nhận
 * `referencePrice` khi vật tư chưa có giá tham khảo.
 */
export interface SupplyResponse {
  id: string;
  code: string;
  name: string;
  group: SupplyGroup;
  unit: Unit;
  minStockLevel: number;
  stockQuantity: number;
  status: RecordStatus;
  note: string | null;
  /** Đã có giao dịch kho, không đổi được đơn vị (BR-CAT-05). */
  unitLocked: boolean;
  referencePrice?: number;
  averageUnitCost?: number;
  /** Gửi kèm khi sửa để không ghi đè thay đổi của người khác (BR-GEN-11). */
  version: number;
}

export interface SupplyListItem {
  id: string;
  code: string;
  name: string;
  group: SupplyGroup;
  unit: Unit;
  minStockLevel: number;
  stockQuantity: number;
  status: RecordStatus;
  referencePrice?: number;
  averageUnitCost?: number;
}

/** Tham số danh sách; `sort`: `code` (mặc định), `name`, `stockQuantity`, `createdAt`, thêm `-` để giảm dần. */
export interface SupplyQuery extends PageQuery {
  group?: SupplyGroup;
  status?: RecordStatus;
  /** Chỉ vật tư đang sử dụng có tồn dưới ngưỡng, vật tư tồn âm xếp trước (BR-SUP-12). */
  belowMin?: boolean;
}

/** Mã để trống thì hệ thống tự sinh dạng `VT001` (BR-CAT-01). Vật tư mới luôn đang sử dụng. */
export interface CreateSupplyRequest {
  code: string | null;
  name: string;
  group: SupplyGroup;
  unit: Unit;
  minStockLevel: number;
  referencePrice: number | null;
  note: string | null;
}

/** Không có trường tồn và giá vốn: tồn đổi qua giao dịch kho, giá vốn do hệ thống cập nhật. */
export interface UpdateSupplyRequest extends CreateSupplyRequest {
  code: string;
  status: RecordStatus;
  version: number;
}
