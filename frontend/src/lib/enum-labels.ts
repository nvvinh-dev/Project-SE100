import type {
  AIFeature,
  CropGroup,
  ExpenseCategory,
  ExportFormat,
  ExtraPermission,
  FarmTaskStatus,
  FarmTaskType,
  PaymentStatus,
  ProduceAdjustmentReason,
  QualityGrade,
  RecordStatus,
  ReportType,
  SalesChannel,
  SeasonStatus,
  SoilType,
  StockTransactionType,
  SupplyGroup,
  Unit,
  UserRole,
  WaterSource,
} from "@/lib/enums";

// Nhãn tiếng Việt của mọi enum, đúng glossary. Không viết nhãn enum ở chỗ khác.

export const userRoleLabels: Record<UserRole, string> = {
  Owner: "Chủ hộ",
  Member: "Thành viên hộ",
  Worker: "Nhân công",
  SystemAdmin: "Quản trị hệ thống",
};

export const extraPermissionLabels: Record<ExtraPermission, string> = {
  ManageStock: "Quản lý kho",
  ManageFinance: "Quản lý tài chính",
  CloseSeason: "Chốt mùa vụ",
};

export const recordStatusLabels: Record<RecordStatus, string> = {
  Active: "Đang sử dụng",
  Inactive: "Ngừng sử dụng",
};

export const soilTypeLabels: Record<SoilType, string> = {
  Alluvial: "Đất phù sa",
  Loam: "Đất thịt",
  Sandy: "Đất cát",
  Clay: "Đất sét",
  RedBasalt: "Đất đỏ bazan",
  AcidSulfateOrSaline: "Đất phèn hoặc mặn",
  Other: "Khác",
};

export const waterSourceLabels: Record<WaterSource, string> = {
  Well: "Giếng",
  RiverCanal: "Sông/kênh/mương",
  PondLake: "Ao/hồ",
  Rainwater: "Nước mưa",
  TapWater: "Nước máy",
  Other: "Khác",
};

export const cropGroupLabels: Record<CropGroup, string> = {
  Staple: "Lúa và cây lương thực",
  Vegetable: "Rau màu",
  FruitTree: "Cây ăn quả",
  IndustrialCrop: "Cây công nghiệp",
  Other: "Khác",
};

export const seasonStatusLabels: Record<SeasonStatus, string> = {
  Planned: "Lên kế hoạch",
  InProgress: "Đang canh tác",
  Closed: "Đã chốt",
  Cancelled: "Đã hủy",
};

export const farmTaskTypeLabels: Record<FarmTaskType, string> = {
  LandPreparation: "Làm đất",
  Sowing: "Gieo trồng",
  Watering: "Tưới nước",
  Fertilizing: "Bón phân",
  Weeding: "Làm cỏ",
  PestInspection: "Kiểm tra sâu bệnh",
  Spraying: "Phun thuốc",
  Other: "Khác",
};

export const farmTaskStatusLabels: Record<FarmTaskStatus, string> = {
  Pending: "Chưa làm",
  WaitingForSupply: "Chờ bổ sung vật tư",
  Done: "Hoàn thành",
  Cancelled: "Đã hủy",
};

export const qualityGradeLabels: Record<QualityGrade, string> = {
  Grade1: "Loại 1",
  Grade2: "Loại 2",
  Grade3: "Loại 3",
};

export const produceAdjustmentReasonLabels: Record<ProduceAdjustmentReason, string> = {
  Loss: "Hao hụt",
  Damaged: "Hư hỏng",
  HouseholdUse: "Gia đình sử dụng",
  KeptForSeed: "Để giống",
  Other: "Khác",
};

export const supplyGroupLabels: Record<SupplyGroup, string> = {
  Seed: "Giống",
  Fertilizer: "Phân bón",
  Pesticide: "Thuốc bảo vệ thực vật",
  IrrigationMaterial: "Vật liệu tưới",
  Consumable: "Hàng tiêu hao khác",
};

export const stockTransactionTypeLabels: Record<StockTransactionType, string> = {
  In: "Nhập kho",
  Out: "Xuất kho",
  Adjustment: "Điều chỉnh",
};

export const expenseCategoryLabels: Record<ExpenseCategory, string> = {
  Utilities: "Điện nước",
  Labor: "Thuê nhân công",
  Transport: "Vận chuyển",
  Other: "Khác",
};

export const salesChannelLabels: Record<SalesChannel, string> = {
  Trader: "Thương lái",
  WholesaleMarket: "Chợ đầu mối",
  Retail: "Bán lẻ",
  Cooperative: "Hợp tác xã",
  Online: "Bán trực tuyến",
  Other: "Khác",
};

export const paymentStatusLabels: Record<PaymentStatus, string> = {
  Unpaid: "Chưa thanh toán",
  PartiallyPaid: "Thanh toán một phần",
  Paid: "Đã thanh toán",
};

export const reportTypeLabels: Record<ReportType, string> = {
  Production: "Báo cáo sản xuất",
  FarmTask: "Báo cáo công việc",
  Supply: "Báo cáo vật tư",
  Finance: "Báo cáo chi phí - doanh thu",
};

export const exportFormatLabels: Record<ExportFormat, string> = {
  Excel: "Excel",
  Pdf: "PDF",
};

export const aiFeatureLabels: Record<AIFeature, string> = {
  Assistant: "Trợ lý hỏi đáp",
  SeasonSummary: "Tóm tắt mùa vụ",
  TaskEntryDraft: "Nhập liệu bằng câu tự nhiên",
  TaskPlanDraft: "Gợi ý lịch công việc",
};

export const unitLabels: Record<Unit, string> = {
  Kilogram: "kg",
  Gram: "g",
  Quintal: "tạ",
  Ton: "tấn",
  Liter: "lít",
  Milliliter: "ml",
  Bag: "bao",
  Bottle: "chai",
  Pack: "gói",
  Plant: "cây",
  Fruit: "quả",
  Bunch: "bó",
};
