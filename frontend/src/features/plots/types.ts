import type { PageQuery } from "@/lib/api-client";
import type { RecordStatus, SoilType, WaterSource } from "@/lib/enums";

// Trùng tên DTO backend (Features/Plots/PlotDtos.cs).

/**
 * Chi tiết một thửa đất. Các trường có thể để trống là vì Nhân công chỉ được thấy mã và vị trí (BR-USR-14);
 * trang thửa đất chỉ dành cho Chủ hộ, Thành viên hộ nên luôn nhận đủ.
 */
export interface PlotResponse {
  id: string;
  code: string;
  areaM2: number | null;
  locationDescription: string | null;
  soilType: SoilType | null;
  soilTypeOther: string | null;
  waterSource: WaterSource | null;
  waterSourceOther: string | null;
  status: RecordStatus | null;
  note: string | null;
  /** Gửi kèm khi sửa để không ghi đè thay đổi của người khác (BR-GEN-11). */
  version: number | null;
}

export interface PlotListItem {
  id: string;
  code: string;
  areaM2: number | null;
  locationDescription: string | null;
  soilType: SoilType | null;
  waterSource: WaterSource | null;
  status: RecordStatus | null;
}

/** Tham số danh sách; `sort`: `code` (mặc định), `areaM2`, `createdAt`, thêm `-` để giảm dần. */
export interface PlotQuery extends PageQuery {
  status?: RecordStatus;
  soilType?: SoilType;
  waterSource?: WaterSource;
}

/** Mã để trống thì hệ thống tự sinh dạng `TD001` (BR-CAT-01). Thửa mới luôn đang sử dụng. */
export interface CreatePlotRequest {
  code: string | null;
  areaM2: number;
  locationDescription: string | null;
  soilType: SoilType;
  soilTypeOther: string | null;
  waterSource: WaterSource;
  waterSourceOther: string | null;
  note: string | null;
}

/** Sửa thửa, kể cả đổi tình trạng sử dụng (FR-PLOT-02). */
export interface UpdatePlotRequest {
  code: string;
  areaM2: number;
  locationDescription: string | null;
  soilType: SoilType;
  soilTypeOther: string | null;
  waterSource: WaterSource;
  waterSourceOther: string | null;
  status: RecordStatus;
  note: string | null;
  version: number;
}
