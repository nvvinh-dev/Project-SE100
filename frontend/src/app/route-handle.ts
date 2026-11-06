import { useMatches } from "react-router";
import type { Access } from "@/lib/auth/access";

/**
 * Khai báo cạnh route trong `features/<module>/routes.tsx`, ví dụ
 * `{ path: "/plots/new", element: <PlotFormPage />, handle: { access: ACCESS.owner, form: true } satisfies RouteHandle }`.
 */
export interface RouteHandle {
  /**
   * Ai được mở trang. Người không được phép vẫn ở đúng đường dẫn nhưng thấy báo "không có quyền", component trang không
   * được dựng nên không gọi API của trang. Dùng cùng điều kiện với mục điều hướng trong `app/navigation.ts`.
   */
  access?: Access;
  /** Trang form tạo, sửa: ẩn thanh tab vì form có thanh nút dính đáy riêng. */
  form?: boolean;
}

/** Gộp `handle` của các route đang khớp: điều kiện quyền lấy của route sâu nhất có khai báo. */
export function mergeRouteHandles(matches: readonly { handle?: unknown }[]): RouteHandle {
  const merged: RouteHandle = {};
  for (const match of matches) {
    const handle = match.handle as RouteHandle | undefined;
    if (handle?.access) {
      merged.access = handle.access;
    }
    if (handle?.form) {
      merged.form = true;
    }
  }
  return merged;
}

/** `handle` đã gộp của trang đang mở. */
export function useRouteHandle(): RouteHandle {
  return mergeRouteHandles(useMatches());
}
