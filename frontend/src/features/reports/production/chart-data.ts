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
  // Lấy tối đa 8 mùa vụ gần nhất theo bộ lọc trước khi tách nhóm theo đơn vị sản lượng (`06-ui-screens.md` mục 8).
  const latestEight = seasons
    .filter((item) => item.season.status !== "Cancelled")
    .sort((a, b) => b.season.startDate.localeCompare(a.season.startDate))
    .slice(0, MAX_CHART_SEASONS)
    .reverse();

  const byUnit = new Map<Unit, ProductionSeasonItem[]>();
  for (const item of latestEight) {
    const list = byUnit.get(item.season.yieldUnit);
    if (list) {
      list.push(item);
    } else {
      byUnit.set(item.season.yieldUnit, [item]);
    }
  }

  const groups: ProductionChartGroup[] = [];
  for (const [unit, items] of byUnit.entries()) {
    groups.push({
      unit,
      points: items.map((item) => {
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
