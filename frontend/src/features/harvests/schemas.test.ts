import { describe, expect, it } from "vitest";
import {
  emptyHarvestFormValues,
  emptyProduceStockAdjustmentFormValues,
  harvestFormSchema,
  harvestToFormValues,
  produceStockAdjustmentFormSchema,
  toCorrectHarvestRequest,
  toCreateHarvestRequest,
  toCreateProduceStockAdjustmentRequest,
} from "@/features/harvests/schemas";
import type { HarvestRecordResponse } from "@/features/harvests/types";
import { todayInVietnam } from "@/features/supplies/schemas";

describe("harvestFormSchema", () => {
  it("chấp nhận dữ liệu phiếu thu hoạch hợp lệ", () => {
    const parsed = harvestFormSchema.safeParse({
      seasonId: "season-1",
      harvestDate: todayInVietnam(),
      qualityGrade: "Grade1",
      quantity: "125,5",
      note: "  Thu đợt 1  ",
    });

    expect(parsed.success).toBe(true);
    if (parsed.success) {
      expect(parsed.data.note).toBe("Thu đợt 1");
    }
  });

  it("từ chối khi thiếu mùa vụ hoặc sản lượng <= 0 hoặc quá 3 chữ số thập phân", () => {
    const missingSeason = harvestFormSchema.safeParse({
      ...emptyHarvestFormValues(),
      quantity: "10",
    });
    expect(missingSeason.success).toBe(false);

    const zeroQty = harvestFormSchema.safeParse({
      ...emptyHarvestFormValues(),
      seasonId: "season-1",
      quantity: "0",
    });
    expect(zeroQty.success).toBe(false);

    const tooManyDecimals = harvestFormSchema.safeParse({
      ...emptyHarvestFormValues(),
      seasonId: "season-1",
      quantity: "10.1234",
    });
    expect(tooManyDecimals.success).toBe(false);
  });

  it("từ chối ngày thu hoạch sau hôm nay", () => {
    const futureDate = harvestFormSchema.safeParse({
      ...emptyHarvestFormValues(),
      seasonId: "season-1",
      harvestDate: "2099-01-01",
      quantity: "5",
    });
    expect(futureDate.success).toBe(false);
  });
});

describe("harvest request mappers", () => {
  it("chuyển đổi CreateHarvestRecordRequest và CorrectHarvestRecordRequest đúng định dạng", () => {
    const output = harvestFormSchema.parse({
      seasonId: "season-1",
      harvestDate: "2026-10-15",
      qualityGrade: "Grade2",
      quantity: "42,75",
      note: "",
    });

    expect(toCreateHarvestRequest(output, "req-1")).toEqual({
      requestId: "req-1",
      seasonId: "season-1",
      harvestDate: "2026-10-15",
      qualityGrade: "Grade2",
      quantity: 42.75,
      note: null,
    });

    expect(toCorrectHarvestRequest(output, "Sửa số cân", 3)).toEqual({
      harvestDate: "2026-10-15",
      qualityGrade: "Grade2",
      quantity: 42.75,
      note: null,
      reason: "Sửa số cân",
      version: 3,
    });
  });

  it("chuyển HarvestRecordResponse sang giá trị form để chỉnh sửa", () => {
    const record: HarvestRecordResponse = {
      id: "h-1",
      seasonId: "s-1",
      cropName: "Lúa",
      plotCode: "T-01",
      seasonStartDate: "2026-08-01",
      seasonStatus: "InProgress",
      yieldUnit: "Kilogram",
      harvestDate: "2026-10-15",
      qualityGrade: "Grade1",
      quantity: 120.5,
      note: null,
      voidedAt: null,
      voidReason: null,
      createdAt: "2026-10-15T08:00:00Z",
      version: 2,
      canModify: true,
      linkedSales: [],
    };

    expect(harvestToFormValues(record)).toEqual({
      seasonId: "s-1",
      harvestDate: "2026-10-15",
      qualityGrade: "Grade1",
      quantity: "120,5",
      note: "",
    });
  });
});

describe("produceStockAdjustmentFormSchema", () => {
  it("chấp nhận điều chỉnh giảm hợp lệ và yêu cầu reasonOther khi chọn Other", () => {
    const valid = produceStockAdjustmentFormSchema.safeParse({
      seasonId: "s-1",
      qualityGrade: "Grade1",
      quantity: "300",
      adjustmentDate: todayInVietnam(),
      reason: "HouseholdUse",
      reasonOther: "",
      note: "  Dùng ăn  ",
    });
    expect(valid.success).toBe(true);

    const missingOther = produceStockAdjustmentFormSchema.safeParse({
      ...emptyProduceStockAdjustmentFormValues({ seasonId: "s-1", qualityGrade: "Grade2" }),
      quantity: "12,5",
      reason: "Other",
      reasonOther: "   ",
    });
    expect(missingOther.success).toBe(false);

    const validOther = produceStockAdjustmentFormSchema.parse({
      seasonId: "s-1",
      qualityGrade: "Grade2",
      quantity: "12,5",
      adjustmentDate: "2026-10-15",
      reason: "Other",
      reasonOther: "  Cho hàng xóm  ",
      note: "",
    });

    expect(toCreateProduceStockAdjustmentRequest(validOther, "req-adj-1")).toEqual({
      requestId: "req-adj-1",
      seasonId: "s-1",
      qualityGrade: "Grade2",
      quantity: 12.5,
      adjustmentDate: "2026-10-15",
      reason: "Other",
      reasonOther: "Cho hàng xóm",
      note: null,
    });
  });
});
