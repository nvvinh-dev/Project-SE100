import { WifiOffIcon } from "lucide-react";
import { useSyncExternalStore } from "react";
import { OFFLINE_ERROR_MESSAGE } from "@/lib/api-error";

function subscribeOnline(onChange: () => void) {
  window.addEventListener("online", onChange);
  window.addEventListener("offline", onChange);
  return () => {
    window.removeEventListener("online", onChange);
    window.removeEventListener("offline", onChange);
  };
}

function useIsOnline() {
  return useSyncExternalStore(subscribeOnline, () => navigator.onLine);
}

/** Dải thông báo dính đầu trang khi mất mạng; gắn một lần ở gốc ứng dụng. */
export function OfflineBanner() {
  const online = useIsOnline();
  if (online) {
    return null;
  }

  return (
    <div
      role="status"
      className="sticky top-0 z-50 flex items-center justify-center gap-2 bg-amber-100 px-4 py-2 text-center text-sm font-medium text-amber-900"
    >
      <WifiOffIcon className="size-4 shrink-0" aria-hidden />
      {OFFLINE_ERROR_MESSAGE}
    </div>
  );
}
