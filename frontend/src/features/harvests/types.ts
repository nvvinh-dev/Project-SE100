import type { PageQuery } from "@/lib/api-client";
import type { ProduceAdjustmentReason, QualityGrade, SalesChannel, SeasonStatus, Unit } from "@/lib/enums";

export interface CreateHarvestRecordRequest {
  requestId: string;
  seasonId: string;
  harvestDate: string;
  qualityGrade: QualityGrade;
  quantity: number;
  note: string | null;
}

export interface CorrectHarvestRecordRequest {
  harvestDate: string;
  qualityGrade: QualityGrade;
  quantity: number;
  note: string | null;
  reason: string;
  version: number;
}

export interface VoidHarvestRecordRequest {
  reason: string;
}

export interface HarvestLinkedSaleItem {
  saleId: string;
  saleDate: string;
  quantity: number;
  customerName?: string | null;
  channel?: SalesChannel | null;
}

export interface HarvestRecordResponse {
  id: string;
  seasonId: string;
  cropName: string;
  plotCode: string;
  seasonStartDate: string;
  seasonStatus: SeasonStatus;
  yieldUnit: Unit;
  harvestDate: string;
  qualityGrade: QualityGrade;
  quantity: number;
  note?: string | null;
  voidedAt?: string | null;
  voidReason?: string | null;
  createdAt: string;
  version: number;
  canModify: boolean;
  linkedSales: HarvestLinkedSaleItem[];
}

export interface HarvestRecordListItem {
  id: string;
  seasonId: string;
  cropName: string;
  plotCode: string;
  seasonStartDate: string;
  yieldUnit: Unit;
  harvestDate: string;
  qualityGrade: QualityGrade;
  quantity: number;
  note?: string | null;
  voidedAt?: string | null;
}

export interface HarvestRecordQuery extends PageQuery {
  seasonId?: string;
  qualityGrade?: QualityGrade;
  from?: string;
  to?: string;
  includeVoided?: boolean;
}

export interface ProduceStockOverviewQuery {
  seasonId?: string;
  qualityGrade?: QualityGrade;
}

export interface ProduceStockOverviewItem {
  seasonId: string;
  cropName: string;
  plotCode: string;
  seasonStartDate: string;
  seasonStatus: SeasonStatus;
  yieldUnit: Unit;
  qualityGrade: QualityGrade;
  totalHarvested: number;
  totalSold: number;
  totalAdjusted: number;
  quantity: number;
}

export interface CreateProduceStockAdjustmentRequest {
  requestId: string;
  seasonId: string;
  qualityGrade: QualityGrade;
  quantity: number;
  adjustmentDate: string;
  reason: ProduceAdjustmentReason;
  reasonOther: string | null;
  note: string | null;
}

export interface VoidProduceStockAdjustmentRequest {
  reason: string;
}

export interface ProduceStockAdjustmentResponse {
  id: string;
  seasonId: string;
  cropName: string;
  plotCode: string;
  seasonStartDate: string;
  seasonStatus: SeasonStatus;
  yieldUnit: Unit;
  qualityGrade: QualityGrade;
  quantity: number;
  adjustmentDate: string;
  reason: ProduceAdjustmentReason;
  reasonOther?: string | null;
  note?: string | null;
  voidedAt?: string | null;
  voidReason?: string | null;
  createdAt: string;
}

export interface ProduceStockAdjustmentQuery extends PageQuery {
  seasonId?: string;
  qualityGrade?: QualityGrade;
  includeVoided?: boolean;
}
