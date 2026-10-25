import type { ReactNode } from "react";

interface PermissionGateProps {
  /** Kết quả kiểm tra quyền của người dùng hiện tại, tính bằng hàm điều kiện quyền dùng chung. */
  allowed: boolean;
  children: ReactNode;
  /** Nội dung thay thế khi không có quyền; mặc định không hiện gì (ẩn hẳn, không làm mờ). */
  fallback?: ReactNode;
}

/** Ẩn nội dung khi không có quyền. Chỉ để tiện dùng: backend vẫn kiểm tra quyền ở mọi API (NFR-04). */
export function PermissionGate({ allowed, children, fallback = null }: PermissionGateProps) {
  return allowed ? children : fallback;
}
