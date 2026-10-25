import { QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import { OfflineBanner } from "@/components/common/OfflineBanner";
import { Toaster } from "@/components/ui/sonner";
import { queryClient } from "@/app/query-client";

export function Providers({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <OfflineBanner />
      {children}
      {/* Giữa phía trên để không che thanh tab và thanh nút ở đáy màn hình điện thoại. */}
      <Toaster position="top-center" richColors containerAriaLabel="Thông báo" />
    </QueryClientProvider>
  );
}
