import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts";
import {
  type ChartConfig,
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
} from "@/components/ui/chart";
import { formatMonth, formatMoneyShort } from "@/features/reports/finance/month";
import type { FinanceMonthItem } from "@/features/reports/finance/types";
import { formatMoney } from "@/lib/format";

// Doanh thu xanh lá, chi phí vàng cam (`06-ui-screens.md` mục 8).
const chartConfig = {
  revenue: { label: "Doanh thu", color: "oklch(0.627 0.194 149.214)" },
  cost: { label: "Chi phí", color: "oklch(0.769 0.188 70.08)" },
} satisfies ChartConfig;

/**
 * Biểu đồ doanh thu và chi phí theo tháng (FR-RPT-FIN-06): cột nhóm, trục từ 0, tiền rút gọn trên trục,
 * chạm vào cột hiện số đầy đủ.
 */
export function MonthlyChart({ items }: { items: readonly FinanceMonthItem[] }) {
  const data = items.map((item) => ({ ...item, label: formatMonth(item.month) }));

  return (
    <ChartContainer config={chartConfig} className="aspect-auto h-64 w-full">
      <BarChart data={data} margin={{ left: 0, right: 8 }}>
        <CartesianGrid vertical={false} />
        <XAxis dataKey="label" tickLine={false} axisLine={false} fontSize={12} />
        <YAxis
          domain={[0, "auto"]}
          width={52}
          tickLine={false}
          axisLine={false}
          fontSize={12}
          tickFormatter={(value: number) => formatMoneyShort(value)}
        />
        <ChartTooltip content={<ChartTooltipContent formatter={(value, name) => (
          <span className="flex w-full justify-between gap-3">
            <span className="text-muted-foreground">{chartConfig[name as keyof typeof chartConfig]?.label ?? name}</span>
            <span className="font-medium tabular-nums">{formatMoney(Number(value))}</span>
          </span>
        )} />} />
        <ChartLegend content={<ChartLegendContent />} />
        <Bar dataKey="revenue" fill="var(--color-revenue)" radius={4} />
        <Bar dataKey="cost" fill="var(--color-cost)" radius={4} />
      </BarChart>
    </ChartContainer>
  );
}
