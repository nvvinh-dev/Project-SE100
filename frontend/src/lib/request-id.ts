import { useState } from "react";

/**
 * `requestId` của form tạo bản ghi giao dịch: sinh một lần khi mở form, bấm gửi lại vẫn dùng đúng giá trị đó
 * để backend nhận ra yêu cầu trùng (BR-GEN-10).
 */
export function useRequestId(): string {
  const [requestId] = useState(newRequestId);
  return requestId;
}

export function newRequestId(): string {
  // crypto.randomUUID chỉ có khi trang chạy qua HTTPS hoặc localhost; mở bằng điện thoại qua mạng LAN (HTTP) thì không có (D-14).
  if (typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }
  return uuidFromRandomBytes(crypto.getRandomValues(new Uint8Array(16)));
}

/** Ghép 16 byte ngẫu nhiên thành UUID phiên bản 4 (RFC 9562). */
export function uuidFromRandomBytes(randomBytes: Uint8Array): string {
  const bytes = Uint8Array.from(randomBytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}
