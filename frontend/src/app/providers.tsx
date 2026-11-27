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
      {/* Giữa phía dưới để không che tiêu đề và nút chính ở đầu trang. Khoảng cách tới đáy lấy từ --toast-bottom
          (index.css) cho cả hai mốc màn hình của sonner, để không bị thanh tab che ở khoảng 600-767px. */}
      <Toaster
        position="bottom-center"
        offset={{ bottom: "var(--toast-bottom)" }}
        mobileOffset={{ bottom: "var(--toast-bottom)" }}
        richColors
        containerAriaLabel="Thông báo"
      />
    </QueryClientProvider>
  );
}
