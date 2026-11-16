import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { buttonVariants } from "@/components/ui/button";
import { type HarvestFilterState, HarvestListSection } from "@/features/harvests/components/HarvestListSection";
import { qualityGrades, type QualityGrade } from "@/lib/enums";

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

/** `/harvests`: danh sách phiếu thu hoạch (FR-HAR-04). */
export function HarvestListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const state = readFilterState(searchParams);

  function updateParams(changes: Partial<HarvestFilterState>) {
    setSearchParams((params) => {
      const next = new URLSearchParams(params);
      const map: Record<string, string | undefined> = {
        page: changes.page ? String(changes.page) : undefined,
        search: changes.search,
        seasonId: changes.seasonId,
        qualityGrade: changes.qualityGrade,
        from: changes.from,
        to: changes.to,
        includeVoided: changes.includeVoided ? "true" : undefined,
      };
      for (const [key, value] of Object.entries(map)) {
        if (key in changes) {
          if (value) {
            next.set(key, value);
          } else {
            next.delete(key);
          }
        }
      }
      if (!("page" in changes)) {
        next.delete("page");
      }
      return next;
    });
  }

  const createHref = state.seasonId ? `/harvests/new?seasonId=${state.seasonId}` : "/harvests/new";

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Thu hoạch"
        action={
          <Link to={createHref} className={buttonVariants({ className: "min-h-11" })}>
            <PlusIcon data-icon="inline-start" />
            Ghi thu hoạch
          </Link>
        }
      />

      <HarvestListSection
        controlledState={state}
        onControlledChange={updateParams}
        emptyMessage="Chưa có phiếu thu hoạch nào. Bấm 'Ghi thu hoạch' để thêm phiếu đầu tiên."
      />
    </div>
  );
}

function readFilterState(params: URLSearchParams): HarvestFilterState {
  const page = Number(params.get("page"));
  const seasonId = matchPattern(params.get("seasonId"), UUID_PATTERN);
  const rawGrade = params.get("qualityGrade");
  const qualityGrade = qualityGrades.find((g) => g === rawGrade) as QualityGrade | undefined;
  const from = matchPattern(params.get("from"), DATE_PATTERN);
  const rawTo = matchPattern(params.get("to"), DATE_PATTERN);
  const to = from && rawTo && rawTo < from ? undefined : rawTo;

  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    search: params.get("search") ?? undefined,
    seasonId,
    qualityGrade,
    from,
    to,
    includeVoided: params.get("includeVoided") === "true" ? true : undefined,
  };
}

function matchPattern(value: string | null, pattern: RegExp): string | undefined {
  return value && pattern.test(value) ? value : undefined;
}
