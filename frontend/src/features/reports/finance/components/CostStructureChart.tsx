import { Bar, BarChart, Cell, LabelList, XAxis, YAxis } from "recharts";
import { type ChartConfig, ChartContainer, ChartTooltip, ChartTooltipContent } from "@/components/ui/chart";
import type { CostRow } from "@/features/reports/finance/cost-rows";
import { formatMoneyShort } from "@/features/reports/finance/month";
import { formatMoney } from "@/lib/format";

// Ba loại chi phí tô ba màu, có chú thích màu bên dưới (`06-ui-screens.md` mục 8).
const KIND_COLORS = {
  supply: "oklch(0.627 0.194 149.214)",
  expense: "oklch(0.769 0.188 70.08)",
  general: "oklch(0.556 0 0)",
} as const;

const KIND_LABELS = {
  supply: "Chi phí vật tư",
  expense: "Chi phí ghi tay",
  general: "Chi phí chung của hộ",
} as const;

type CostKind = keyof typeof KIND_COLORS;

function kindOf(row: CostRow): CostKind {
  if (row.key.startsWith("supply-")) return "supply";
  if (row.key.startsWith("expense-")) return "expense";
  return "general";
}

const chartConfig = { amount: { label: "Số tiền" } } satisfies ChartConfig;

/**
 * Biểu đồ cơ cấu chi phí theo nhóm (FR-RPT-FIN-05): cột ngang sắp giảm dần, trục từ 0, nhãn số tiền rút gọn ở cuối cột,
 * chạm vào cột hiện số tiền đầy đủ.
 */
export function CostStructureChart({ rows }: { rows: readonly CostRow[] }) {
  const data = rows
    .filter((row) => row.amount > 0)
    .map((row) => ({ ...row, label: row.label.replace(/^(Vật tư|Chi phí ghi tay): /, ""), kind: kindOf(row) }));
  if (data.length === 0) {
    return null;
  }
  const kinds = [...new Set(data.map((item) => item.kind))];

  return (
    <div className="flex flex-col gap-2">
      <ChartContainer config={chartConfig} className="aspect-auto w-full" style={{ height: data.length * 44 + 24 }}>
        <BarChart data={data} layout="vertical" margin={{ left: 0, right: 56 }}>
          <XAxis type="number" domain={[0, "auto"]} hide />
          <YAxis type="category" dataKey="label" width={112} tickLine={false} axisLine={false} interval={0} fontSize={12} />
          <ChartTooltip
            cursor={false}
            content={<ChartTooltipContent hideIndicator formatter={(value) => formatMoney(Number(value))} />}
          />
          <Bar dataKey="amount" radius={4}>
            {data.map((item) => (
              <Cell key={item.key} fill={KIND_COLORS[item.kind]} />
            ))}
            <LabelList
              dataKey="amount"
              position="right"
              className="fill-foreground"
              fontSize={12}
              formatter={(value: unknown) => formatMoneyShort(Number(value))}
            />
          </Bar>
        </BarChart>
      </ChartContainer>
      <ul className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground" aria-label="Chú thích màu">
        {kinds.map((kind) => (
          <li key={kind} className="inline-flex items-center gap-1.5">
            <span className="size-2.5 rounded-sm" style={{ backgroundColor: KIND_COLORS[kind] }} aria-hidden />
            {KIND_LABELS[kind]}
          </li>
        ))}
      </ul>
    </div>
  );
}
