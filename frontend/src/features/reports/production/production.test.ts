import { describe, expect, it } from "vitest";
import { applyFarmTaskFilterParams, readFarmTaskReportQuery } from "@/features/reports/farm-tasks/filter";
import { buildProductionChartGroups } from "@/features/reports/production/chart-data";
import {
  applyProductionFilterParams,
  DEFAULT_DEVIATION_THRESHOLD,
  readProductionReportQuery,
} from "@/features/reports/production/filter";
import type { ProductionSeasonItem } from "@/features/reports/production/types";
import type { Unit } from "@/lib/enums";

function makeSeasonItem(id: string, startDate: string, yieldUnit: Unit = "Kilogram"): ProductionSeasonItem {
  return {
    season: {
      seasonId: id,
      plotId: "p1",
      plotCode: "TD001",
      cropId: "c1",
      cropName: "Lúa OM5451",
      yieldUnit,
      areaM2: 1000,
      startDate,
      expectedHarvestDate: "2026-12-31",
      status: "Closed",
      isProvisional: false,
    },
    actualYield: 1200,
    targetYield: 1000,
    yieldDeviation: 20,
    isHighDeviation: true,
    yieldPerM2: 1.2,
    yieldPerHectare: 12000,
    supplyCost: null,
    revenue: null,
    profit: null,
    previousComparison: null,
  };
}

describe("production report filter & chart helpers", () => {
  it("reads defaults and serializes custom deviation threshold, onlyHighDeviation and compareBy", () => {
    const empty = readProductionReportQuery(new URLSearchParams());
    expect(empty.deviationThreshold).toBe(DEFAULT_DEVIATION_THRESHOLD);
    expect(empty.onlyHighDeviation).toBe(false);
    expect(empty.compareBy).toBe("SamePlot");

    const updated = applyProductionFilterParams(new URLSearchParams(), {
      deviationThreshold: 30,
      onlyHighDeviation: true,
      compareBy: "SameCrop",
    });
    expect(updated.toString()).toBe("deviationThreshold=30&onlyHighDeviation=true&compareBy=SameCrop");

    const parsed = readProductionReportQuery(updated);
    expect(parsed.deviationThreshold).toBe(30);
    expect(parsed.onlyHighDeviation).toBe(true);
    expect(parsed.compareBy).toBe("SameCrop");
  });

  it("groups chart data by unit and limits each group to at most 8 most recent seasons", () => {
    const kgSeasons = Array.from({ length: 10 }, (_, idx) => {
      const month = String(idx + 1).padStart(2, "0");
      return makeSeasonItem(`s-${idx + 1}`, `2026-${month}-01`, "Kilogram");
    });
    const tonSeason = makeSeasonItem("s-ton", "2026-05-15", "Ton");

    const groups = buildProductionChartGroups([...kgSeasons, tonSeason]);
    expect(groups).toHaveLength(2);

    const kgGroup = groups.find((g) => g.unit === "Kilogram")!;
    expect(kgGroup.points).toHaveLength(8);
    expect(kgGroup.points[0].seasonId).toBe("s-3");
    expect(kgGroup.points[7].seasonId).toBe("s-10");

    const tonGroup = groups.find((g) => g.unit === "Ton")!;
    expect(tonGroup.points).toHaveLength(1);
    expect(tonGroup.points[0].seasonId).toBe("s-ton");
  });
});

describe("farm-task report filter helpers", () => {
  it("reads and writes groupBy cleanly", () => {
    expect(readFarmTaskReportQuery(new URLSearchParams()).groupBy).toBe("Time");

    const params = applyFarmTaskFilterParams(new URLSearchParams(), { groupBy: "Assignee" });
    expect(params.get("groupBy")).toBe("Assignee");
    expect(readFarmTaskReportQuery(params).groupBy).toBe("Assignee");

    const reset = applyFarmTaskFilterParams(params, { groupBy: "Time" });
    expect(reset.has("groupBy")).toBe(false);
  });
});
