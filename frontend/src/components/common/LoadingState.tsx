import { Skeleton } from "@/components/ui/skeleton";

interface LoadingStateProps {
  /** Bố cục mô phỏng theo loại trang: danh sách, chi tiết, form. */
  variant?: "list" | "detail" | "form";
  /** Số thẻ, số hàng của bố cục danh sách. */
  rows?: number;
}

/** Khung xám mô phỏng bố cục khi đang tải lần đầu, thay cho màn hình trắng. */
export function LoadingState({ variant = "list", rows = 5 }: LoadingStateProps) {
  return (
    <div role="status" aria-live="polite" className="flex flex-col gap-3">
      <span className="sr-only">Đang tải</span>
      {variant === "list" && <ListSkeleton rows={rows} />}
      {variant === "detail" && <DetailSkeleton />}
      {variant === "form" && <FormSkeleton />}
    </div>
  );
}

function ListSkeleton({ rows }: { rows: number }) {
  const items = Array.from({ length: rows }, (_, index) => index);
  return (
    <>
      <ul className="flex flex-col gap-2 md:hidden">
        {items.map((index) => (
          <li key={index} className="flex items-start gap-3 rounded-lg border p-3">
            <div className="flex flex-1 flex-col gap-2">
              <Skeleton className="h-4 w-3/5" />
              <Skeleton className="h-3 w-2/5" />
            </div>
            <Skeleton className="h-5 w-20 rounded-full" />
          </li>
        ))}
      </ul>
      <div className="hidden flex-col rounded-lg border md:flex">
        <div className="flex gap-4 border-b p-3">
          <Skeleton className="h-4 w-1/4" />
          <Skeleton className="h-4 w-1/4" />
          <Skeleton className="h-4 w-1/6" />
        </div>
        {items.map((index) => (
          <div key={index} className="flex gap-4 border-b p-3 last:border-b-0">
            <Skeleton className="h-4 w-1/4" />
            <Skeleton className="h-4 w-1/4" />
            <Skeleton className="h-4 w-1/6" />
          </div>
        ))}
      </div>
    </>
  );
}

function DetailSkeleton() {
  return (
    <>
      <Skeleton className="h-7 w-1/2" />
      <Skeleton className="h-5 w-24 rounded-full" />
      <div className="flex flex-col gap-3 rounded-lg border p-4">
        {[0, 1, 2, 3].map((index) => (
          <div key={index} className="flex justify-between gap-4">
            <Skeleton className="h-4 w-1/3" />
            <Skeleton className="h-4 w-1/4" />
          </div>
        ))}
      </div>
      <Skeleton className="h-11 w-full md:h-8 md:w-40" />
    </>
  );
}

function FormSkeleton() {
  return (
    <>
      {[0, 1, 2, 3].map((index) => (
        <div key={index} className="flex flex-col gap-2">
          <Skeleton className="h-4 w-28" />
          <Skeleton className="h-11 w-full md:h-8" />
        </div>
      ))}
      <Skeleton className="h-11 w-full md:h-8 md:w-40" />
    </>
  );
}
