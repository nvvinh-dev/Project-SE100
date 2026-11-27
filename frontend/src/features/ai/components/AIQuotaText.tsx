import { useAIQuota } from "@/features/ai/queries";

/** Dòng "Còn 42/50 lượt hôm nay." Chưa tải được số lượt thì không hiện gì: số lượt chỉ để tham khảo, máy chủ vẫn chặn. */
export function AIQuotaText() {
  const quota = useAIQuota();

  if (!quota.data) {
    return null;
  }

  return (
    <p className="text-sm text-muted-foreground tabular-nums">
      Còn {quota.data.remaining}/{quota.data.limit} lượt hôm nay.
    </p>
  );
}
