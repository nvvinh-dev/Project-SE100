import { Bar, BarChart, CartesianGrid, XAxis, YAxis } from "recharts";
import { QuantityText } from "@/components/common/QuantityText";
import {
  type ChartConfig,
  ChartContainer,
  ChartLegend,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
} from "@/components/ui/chart";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { buildProductionChartGroups, type ProductionChartGroup } from "@/features/reports/production/chart-data";
import type { ProductionSeasonItem } from "@/features/reports/production/types";
import { unitLabels } from "@/lib/enum-labels";
import { formatQuantity } from "@/lib/format";

// Mục tiêu màu xám, thực tế màu xanh lá (`06-ui-screens.md` mục 8).
const chartConfig = {
  targetYield: { label: "Mục tiêu", color: "oklch(0.704 0.04 256.788)" },
  actualYield: { label: "Thực tế", color: "oklch(0.627 0.194 149.214)" },
} satisfies ChartConfig;

const axisNumberFormat = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });

function UnitChartBlock({ group }: { group: ProductionChartGroup }) {
  const unitLabel = unitLabels[group.unit];

  return (
    <div className="flex flex-col gap-3 rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="text-sm font-semibold">Đơn vị sản lượng: {unitLabel}</h3>
        <span className="text-xs text-muted-foreground">Hiển thị tối đa 8 mùa vụ gần nhất</span>
      </div>

      <ChartContainer config={chartConfig} className="aspect-auto h-64 w-full">
        <BarChart data={group.points} margin={{ left: 0, right: 8 }}>
          <CartesianGrid vertical={false} />
          <XAxis dataKey="shortLabel" tickLine={false} axisLine={false} fontSize={12} />
          <YAxis
            domain={[0, "auto"]}
            width={56}
            tickLine={false}
            axisLine={false}
            fontSize={12}
            tickFormatter={(value: number) => axisNumberFormat.format(value)}
          />
          <ChartTooltip
            content={
              <ChartTooltipContent
                labelFormatter={(_, payload) => {
                  const point = payload?.[0]?.payload as { label?: string } | undefined;
                  return point?.label ?? "";
                }}
                formatter={(value, name) => (
                  <span className="flex w-full justify-between gap-3">
                    <span className="text-muted-foreground">
                      {chartConfig[name as keyof typeof chartConfig]?.label ?? name}
                    </span>
                    <span className="font-medium tabular-nums">
                      {formatQuantity(Number(value), group.unit)}
                    </span>
                  </span>
                )}
              />
            }
          />
          <ChartLegend content={<ChartLegendContent />} />
          <Bar dataKey="targetYield" fill="var(--color-targetYield)" radius={4} />
          <Bar dataKey="actualYield" fill="var(--color-actualYield)" radius={4} />
        </BarChart>
      </ChartContainer>

      {/* Bảng số liệu đi kèm ngay bên dưới để đọc chính xác trên màn hình điện thoại (`06-ui-screens.md` mục 8). */}
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Mùa vụ</TableHead>
            <TableHead className="text-right">Mục tiêu</TableHead>
            <TableHead className="text-right">Thực tế</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {group.points.map((point) => (
            <TableRow key={point.seasonId}>
              <TableCell className="font-medium">{point.label}</TableCell>
              <TableCell className="text-right">
                <QuantityText value={point.targetYield} unit={group.unit} />
              </TableCell>
              <TableCell className="text-right">
                <QuantityText value={point.actualYield} unit={group.unit} />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}

/**
 * Biểu đồ sản lượng thực tế so với mục tiêu theo mùa vụ (FR-RPT-PROD-05):
 * cột nhóm, trục từ 0, tách riêng khi có nhiều đơn vị sản lượng.
 */
export function ProductionYieldChart({ seasons }: { seasons: readonly ProductionSeasonItem[] }) {
  const groups = buildProductionChartGroups(seasons);
  if (groups.length === 0) {
    return null;
  }

  return (
    <section className="flex flex-col gap-3">
      <div>
        <h2 className="text-base font-semibold">Biểu đồ sản lượng thực tế so với mục tiêu</h2>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Mỗi mùa vụ một cặp cột mục tiêu và thực tế. Mùa vụ chưa chốt ghi nhãn (tạm tính).
        </p>
      </div>
      <div className="flex flex-col gap-4">
        {groups.map((group) => (
          <UnitChartBlock key={group.unit} group={group} />
        ))}
      </div>
    </section>
  );
}
