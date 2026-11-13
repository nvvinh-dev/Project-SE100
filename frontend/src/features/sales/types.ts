import type { PageQuery } from "@/lib/api-client";
import type { RecordStatus } from "@/lib/enums";

// Trùng tên DTO backend (Features/Sales/CustomerDtos.cs).

export interface CustomerResponse {
  id: string;
  name: string;
  phone: string | null;
  note: string | null;
  status: RecordStatus;
  createdAt: string;
  /** Gửi kèm khi sửa để không ghi đè thay đổi của người khác (BR-GEN-11). */
  version: number;
}

export interface CustomerListItem {
  id: string;
  name: string;
  phone: string | null;
  status: RecordStatus;
}

/** Tham số danh sách; `search` tìm theo tên và số điện thoại, luôn sắp theo tên. */
export interface CustomerQuery extends PageQuery {
  status?: RecordStatus;
}

export interface CreateCustomerRequest {
  name: string;
  phone: string | null;
  note: string | null;
  status: RecordStatus;
}

export interface UpdateCustomerRequest extends CreateCustomerRequest {
  version: number;
}
