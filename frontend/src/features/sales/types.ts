import type { PageQuery } from "@/lib/api-client";
import type { PaymentStatus, QualityGrade, RecordStatus, SalesChannel, SeasonStatus, Unit } from "@/lib/enums";

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

// Trùng tên DTO backend (Features/Sales/SaleDtos.cs).

export interface SaleHarvestLinkItem {
  harvestRecordId: string;
  harvestDate: string;
  quantity: number;
}

/** Doanh thu, đã thu, công nợ, trạng thái thanh toán là giá trị tính ở backend, không tự tính lại. */
export interface SaleResponse {
  id: string;
  seasonId: string;
  cropName: string;
  plotCode: string;
  seasonStartDate: string;
  seasonStatus: SeasonStatus;
  yieldUnit: Unit;
  customerId: string | null;
  customerName: string | null;
  channel: SalesChannel | null;
  channelOther: string | null;
  qualityGrade: QualityGrade;
  quantity: number;
  unitPrice: number;
  saleDate: string;
  note: string | null;
  revenue: number;
  paidAmount: number;
  outstandingAmount: number;
  paymentStatus: PaymentStatus;
  harvestLinks: SaleHarvestLinkItem[];
  /** Gồm cả thanh toán đã hủy (lịch sử); đã thu chỉ tính thanh toán chưa hủy. */
  payments: PaymentResponse[];
  voidedAt: string | null;
  voidReason: string | null;
  createdAt: string;
  version: number;
}

export interface SaleListItem {
  id: string;
  saleDate: string;
  seasonId: string;
  cropName: string;
  plotCode: string;
  customerId: string | null;
  customerName: string | null;
  channel: SalesChannel | null;
  channelOther: string | null;
  qualityGrade: QualityGrade;
  quantity: number;
  yieldUnit: Unit;
  revenue: number;
  paidAmount: number;
  outstandingAmount: number;
  paymentStatus: PaymentStatus;
  voidedAt: string | null;
}

/** Tham số danh sách; `from`, `to` lọc theo ngày bán. */
export interface SaleQuery extends PageQuery {
  seasonId?: string;
  customerId?: string;
  channel?: SalesChannel;
  paymentStatus?: PaymentStatus;
  from?: string;
  to?: string;
  includeVoided?: boolean;
}

export interface CreateSaleRequest {
  requestId: string;
  seasonId: string;
  customerId: string | null;
  channel: SalesChannel | null;
  channelOther: string | null;
  qualityGrade: QualityGrade;
  quantity: number;
  unitPrice: number;
  saleDate: string;
  note: string | null;
  harvestRecordIds: string[];
}

/** Điều chỉnh giao dịch bán (BR-SAL-03): không đổi mùa vụ; `harvestRecordIds` thay toàn bộ liên kết cũ. */
export interface CorrectSaleRequest {
  customerId: string | null;
  channel: SalesChannel | null;
  channelOther: string | null;
  qualityGrade: QualityGrade;
  quantity: number;
  unitPrice: number;
  saleDate: string;
  note: string | null;
  harvestRecordIds: string[];
  reason: string;
  version: number;
}

export interface VoidSaleRequest {
  reason: string;
}

// Trùng tên DTO backend (Features/Sales/PaymentDtos.cs).

export interface PaymentResponse {
  id: string;
  saleId: string;
  amount: number;
  paymentDate: string;
  note: string | null;
  voidedAt: string | null;
  voidReason: string | null;
  createdAt: string;
}

export interface CreatePaymentRequest {
  requestId: string;
  saleId: string;
  amount: number;
  paymentDate: string;
  note: string | null;
}

export interface VoidPaymentRequest {
  reason: string;
}

/** Công nợ bán hàng chưa thu (FR-SAL-07). */
export interface SaleDebtsResponse {
  totalCount: number;
  totalOutstanding: number;
  items: SaleListItem[];
}
