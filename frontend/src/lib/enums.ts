// Enum dùng chung, đúng tên trong glossary và trùng giá trị chuỗi backend gửi về.
// Mỗi enum có mảng giá trị (dùng cho ô chọn, schema Zod) và kiểu union suy ra từ mảng đó.

// Người dùng và phân quyền
export const userRoles = ["Owner", "Member", "Worker", "SystemAdmin"] as const;
export type UserRole = (typeof userRoles)[number];

export const extraPermissions = ["ManageStock", "ManageFinance", "CloseSeason"] as const;
export type ExtraPermission = (typeof extraPermissions)[number];

// Thửa đất và cây trồng
export const recordStatuses = ["Active", "Inactive"] as const;
export type RecordStatus = (typeof recordStatuses)[number];

export const soilTypes = ["Alluvial", "Loam", "Sandy", "Clay", "RedBasalt", "AcidSulfateOrSaline", "Other"] as const;
export type SoilType = (typeof soilTypes)[number];

export const waterSources = ["Well", "RiverCanal", "PondLake", "Rainwater", "TapWater", "Other"] as const;
export type WaterSource = (typeof waterSources)[number];

export const cropGroups = ["Staple", "Vegetable", "FruitTree", "IndustrialCrop", "Other"] as const;
export type CropGroup = (typeof cropGroups)[number];

// Mùa vụ và công việc
export const seasonStatuses = ["Planned", "InProgress", "Closed", "Cancelled"] as const;
export type SeasonStatus = (typeof seasonStatuses)[number];

export const farmTaskTypes = [
  "LandPreparation",
  "Sowing",
  "Watering",
  "Fertilizing",
  "Weeding",
  "PestInspection",
  "Spraying",
  "Other",
] as const;
export type FarmTaskType = (typeof farmTaskTypes)[number];

export const farmTaskStatuses = ["Pending", "WaitingForSupply", "Done", "Cancelled"] as const;
export type FarmTaskStatus = (typeof farmTaskStatuses)[number];

// Thu hoạch và nông sản
export const qualityGrades = ["Grade1", "Grade2", "Grade3"] as const;
export type QualityGrade = (typeof qualityGrades)[number];

export const produceAdjustmentReasons = ["Loss", "Damaged", "HouseholdUse", "KeptForSeed", "Other"] as const;
export type ProduceAdjustmentReason = (typeof produceAdjustmentReasons)[number];

// Vật tư và kho
export const supplyGroups = ["Seed", "Fertilizer", "Pesticide", "IrrigationMaterial", "Consumable"] as const;
export type SupplyGroup = (typeof supplyGroups)[number];

export const stockTransactionTypes = ["In", "Out", "Adjustment"] as const;
export type StockTransactionType = (typeof stockTransactionTypes)[number];

// Chi phí, bán hàng, thanh toán
export const expenseCategories = ["Utilities", "Labor", "Transport", "Other"] as const;
export type ExpenseCategory = (typeof expenseCategories)[number];

export const salesChannels = ["Trader", "WholesaleMarket", "Retail", "Cooperative", "Online", "Other"] as const;
export type SalesChannel = (typeof salesChannels)[number];

export const paymentStatuses = ["Unpaid", "PartiallyPaid", "Paid"] as const;
export type PaymentStatus = (typeof paymentStatuses)[number];

// Báo cáo và AI
export const reportTypes = ["Production", "FarmTask", "Supply", "Finance"] as const;
export type ReportType = (typeof reportTypes)[number];

export const exportFormats = ["Excel", "Pdf"] as const;
export type ExportFormat = (typeof exportFormats)[number];

export const aiFeatures = ["Assistant", "SeasonSummary", "TaskEntryDraft", "TaskPlanDraft"] as const;
export type AIFeature = (typeof aiFeatures)[number];

// Đơn vị tính, dùng chung cho vật tư và sản lượng
export const units = [
  "Kilogram",
  "Gram",
  "Quintal",
  "Ton",
  "Liter",
  "Milliliter",
  "Bag",
  "Bottle",
  "Pack",
  "Plant",
  "Fruit",
  "Bunch",
] as const;
export type Unit = (typeof units)[number];
