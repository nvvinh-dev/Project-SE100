import type { PageQuery } from "@/lib/api-client";
import type { CropGroup, RecordStatus, Unit } from "@/lib/enums";

// Trùng tên DTO backend (Features/Crops/CropDtos.cs).

/**
 * Chi tiết một cây trồng. Các trường có thể để trống là vì Nhân công chỉ được thấy tên cây (BR-USR-14);
 * trang cây trồng chỉ dành cho Chủ hộ, Thành viên hộ nên luôn nhận đủ.
 */
export interface CropResponse {
  id: string;
  name: string;
  group: CropGroup | null;
  groupOther: string | null;
  yieldUnit: Unit | null;
  referenceInfo: string | null;
  status: RecordStatus | null;
  /** Cây đã có phiếu thu hoạch nên không đổi được đơn vị sản lượng (BR-CAT-05). */
  yieldUnitLocked: boolean | null;
  /** Gửi kèm khi sửa để không ghi đè thay đổi của người khác (BR-GEN-11). */
  version: number | null;
}

export interface CropListItem {
  id: string;
  name: string;
  group: CropGroup | null;
  yieldUnit: Unit | null;
  status: RecordStatus | null;
}

/** Tham số danh sách; `sort`: `name` (mặc định), `createdAt`, thêm `-` để giảm dần. */
export interface CropQuery extends PageQuery {
  group?: CropGroup;
  status?: RecordStatus;
}

/** Cây mới luôn đang sử dụng. */
export interface CreateCropRequest {
  name: string;
  group: CropGroup;
  groupOther: string | null;
  yieldUnit: Unit;
  referenceInfo: string | null;
}

/** Sửa cây, kể cả đổi trạng thái (FR-CROP-03). */
export interface UpdateCropRequest extends CreateCropRequest {
  status: RecordStatus;
  version: number;
}
