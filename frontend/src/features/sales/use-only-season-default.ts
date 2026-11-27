import { useEffect } from "react";
import { useSearchParams } from "react-router";
import { useSeasonOptions } from "@/features/seasons/queries";
import type { SeasonOperation, SeasonOptionItem } from "@/features/seasons/types";

/**
 * Form tạo mới: hộ chỉ có đúng một mùa vụ chọn được cho thao tác này thì chọn sẵn mùa vụ đó, khỏi bắt người dùng chọn
 * lại điều đã rõ. Đường dẫn có `?seasonId=` thì để ô chọn mùa vụ tự xử lý. Dùng cùng query với ô chọn nên không gọi thêm API.
 */
export function useOnlySeasonDefault(
  operation: SeasonOperation,
  value: string,
  select: (option: SeasonOptionItem) => void,
  enabled = true,
) {
  const [searchParams] = useSearchParams();
  const hasQuerySeason = searchParams.has("seasonId");
  const options = useSeasonOptions({ operation });

  useEffect(() => {
    if (!enabled || value || hasQuerySeason || options.data?.length !== 1) {
      return;
    }
    select(options.data[0]);
  }, [enabled, value, hasQuerySeason, options.data, select]);
}
