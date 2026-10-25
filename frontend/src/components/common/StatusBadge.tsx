import { Badge } from "@/components/ui/badge";
import {
  farmTaskStatusLabels,
  paymentStatusLabels,
  qualityGradeLabels,
  recordStatusLabels,
  seasonStatusLabels,
} from "@/lib/enum-labels";
import type { FarmTaskStatus, PaymentStatus, QualityGrade, RecordStatus, SeasonStatus } from "@/lib/enums";
import { cn } from "@/lib/utils";

type Tone = "slate" | "blue" | "green" | "amber" | "red";

// Nền nhạt, chữ đậm cùng tông; màu luôn đi kèm chữ.
const toneClasses: Record<Tone, string> = {
  slate: "bg-slate-100 text-slate-700",
  blue: "bg-blue-100 text-blue-800",
  green: "bg-green-100 text-green-800",
  amber: "bg-amber-100 text-amber-800",
  red: "bg-red-100 text-red-800",
};

const seasonStatusTones: Record<SeasonStatus, Tone> = {
  Planned: "slate",
  InProgress: "green",
  Closed: "blue",
  Cancelled: "slate",
};

const farmTaskStatusTones: Record<FarmTaskStatus, Tone> = {
  Pending: "blue",
  WaitingForSupply: "amber",
  Done: "green",
  Cancelled: "slate",
};

const paymentStatusTones: Record<PaymentStatus, Tone> = {
  Unpaid: "red",
  PartiallyPaid: "amber",
  Paid: "green",
};

const recordStatusTones: Record<RecordStatus, Tone> = {
  Active: "green",
  Inactive: "slate",
};

// Nhãn tính từ dữ liệu, không phải giá trị enum.
const derivedBadges = {
  /** Nhãn thêm, hiện cạnh nhãn trạng thái của công việc (`IsOverdue`). */
  Overdue: { label: "Trễ hạn", tone: "red" },
  /** Bản ghi đã `Void`. */
  Voided: { label: "Đã hủy", tone: "slate" },
  /** Tồn vật tư dưới `MinStockLevel`. */
  BelowMinStock: { label: "Sắp hết", tone: "amber" },
  NegativeStock: { label: "Tồn âm", tone: "red" },
} as const satisfies Record<string, { label: string; tone: Tone }>;

export type StatusBadgeProps =
  | { kind: "SeasonStatus"; value: SeasonStatus }
  | { kind: "FarmTaskStatus"; value: FarmTaskStatus }
  | { kind: "PaymentStatus"; value: PaymentStatus }
  | { kind: "RecordStatus"; value: RecordStatus }
  | { kind: "QualityGrade"; value: QualityGrade }
  | { kind: keyof typeof derivedBadges };

function resolveBadge(props: Exclude<StatusBadgeProps, { kind: "QualityGrade" }>): { label: string; tone: Tone } {
  switch (props.kind) {
    case "SeasonStatus":
      return { label: seasonStatusLabels[props.value], tone: seasonStatusTones[props.value] };
    case "FarmTaskStatus":
      return { label: farmTaskStatusLabels[props.value], tone: farmTaskStatusTones[props.value] };
    case "PaymentStatus":
      return { label: paymentStatusLabels[props.value], tone: paymentStatusTones[props.value] };
    case "RecordStatus":
      return { label: recordStatusLabels[props.value], tone: recordStatusTones[props.value] };
    default:
      return derivedBadges[props.kind];
  }
}

/** Nhãn trạng thái. Ví dụ `<StatusBadge kind="SeasonStatus" value="InProgress" />`, `<StatusBadge kind="Overdue" />`. */
export function StatusBadge(props: StatusBadgeProps) {
  // Loại chất lượng không mang nghĩa tốt, xấu cần cảnh báo nên dùng nhãn viền, không tô màu.
  if (props.kind === "QualityGrade") {
    return <Badge variant="outline">{qualityGradeLabels[props.value]}</Badge>;
  }
  const { label, tone } = resolveBadge(props);
  return <Badge className={cn(toneClasses[tone])}>{label}</Badge>;
}
