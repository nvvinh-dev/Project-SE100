import { QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { OfflineBanner } from "@/components/common/OfflineBanner";
import { Toaster } from "@/components/ui/sonner";
import { queryClient } from "@/app/query-client";
import { AuthProvider } from "@/lib/auth/AuthProvider";

export function Providers({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <OfflineBanner />
      <AuthProvider>{children}</AuthProvider>
      {/* Giữa phía dưới để không che tiêu đề và nút chính ở đầu trang; trên điện thoại đẩy lên trên thanh tab
          (64px) hoặc thanh nút dính đáy của form, cộng vùng an toàn của màn hình tai thỏ. */}
      <Toaster
        position="bottom-center"
        mobileOffset={{ bottom: "calc(5rem + env(safe-area-inset-bottom))" }}
        richColors
        containerAriaLabel="Thông báo"
      />
    </QueryClientProvider>
  );
}
