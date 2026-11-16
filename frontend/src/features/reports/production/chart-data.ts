import type { ProductionSeasonItem } from "@/features/reports/production/types";
import type { Unit } from "@/lib/enums";

const MAX_CHART_SEASONS = 8;

export interface ProductionChartPoint {
  seasonId: string;
  label: string;
  shortLabel: string;
  targetYield: number;
  actualYield: number;
  isProvisional: boolean;
}

export interface ProductionChartGroup {
  unit: Unit;
  points: ProductionChartPoint[];
}

/**
 * Nhóm các mùa vụ theo đơn vị sản lượng để không trộn đơn vị trên cùng trục biểu đồ,
 * mỗi nhóm lấy tối đa 8 mùa vụ gần nhất theo ngày bắt đầu (FR-RPT-PROD-05).
 */
export function buildProductionChartGroups(
  seasons: readonly ProductionSeasonItem[],
): ProductionChartGroup[] {
  const byUnit = new Map<Unit, ProductionSeasonItem[]>();

  for (const item of seasons) {
    if (item.season.status === "Cancelled") {
      continue;
    }
    const list = byUnit.get(item.season.yieldUnit);
    if (list) {
      list.push(item);
    } else {
      byUnit.set(item.season.yieldUnit, [item]);
    }
  }

  const groups: ProductionChartGroup[] = [];
  for (const [unit, items] of byUnit.entries()) {
    const latestFirst = [...items].sort((a, b) =>
      b.season.startDate.localeCompare(a.season.startDate),
    );
    const sliced = latestFirst.slice(0, MAX_CHART_SEASONS).reverse();

    groups.push({
      unit,
      points: sliced.map((item) => {
        const baseName = `${item.season.cropName} · ${item.season.plotCode}`;
        return {
          seasonId: item.season.seasonId,
          label: item.season.isProvisional ? `${baseName} (tạm tính)` : baseName,
          shortLabel: `${item.season.plotCode} (${item.season.startDate.slice(5, 7)}/${item.season.startDate.slice(2, 4)})`,
          targetYield: item.targetYield,
          actualYield: item.actualYield,
          isProvisional: item.season.isProvisional,
        };
      }),
    });
  }

  return groups;
}
