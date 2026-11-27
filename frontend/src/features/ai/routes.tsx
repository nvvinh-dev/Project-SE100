import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { AssistantPage } from "@/features/ai/pages/AssistantPage";
import { ACCESS } from "@/lib/auth/access";

/**
 * Trang trợ lý AI, cùng điều kiện với mục "Trợ lý AI" trong app/navigation.ts. Ẩn thanh tab như trang form để ô nhập
 * câu hỏi dính đáy màn hình.
 */
export const aiRoutes: RouteObject[] = [
  { path: "/ai", element: <AssistantPage />, handle: { access: ACCESS.household, form: true } satisfies RouteHandle },
];
