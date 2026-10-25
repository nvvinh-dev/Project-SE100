import { unitLabels } from "@/lib/enum-labels";
import type { Unit } from "@/lib/enums";

// Khoảng trắng không ngắt dòng giữa số và đơn vị, để "12,5 kg" không bị tách hai dòng trên màn hình hẹp.
const NBSP = " ";
const VIETNAM_TIME_ZONE = "Asia/Ho_Chi_Minh";
const SQUARE_METERS_PER_HECTARE = 10_000;

// VND hiển thị tới đồng; signDisplay "negative" để số âm làm tròn về 0 không hiện "-0 ₫".
const moneyFormat = new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND", signDisplay: "negative" });
// Số chữ số lẻ tối đa theo kiểu cột trong database: số lượng 3, diện tích m² 2.
const quantityFormat = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 3 });
const areaM2Format = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });
const areaHaFormat = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 4 });
const dateTimeFormat = new Intl.DateTimeFormat("vi-VN", {
  timeZone: VIETNAM_TIME_ZONE,
  day: "2-digit",
  month: "2-digit",
  year: "numeric",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
});

const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/;
// Một dấu thập phân "," hoặc ".", không có dấu phân cách hàng nghìn.
const DECIMAL_INPUT = /^-?\d+(?:[.,]\d+)?$/;

/** `1250000` → `1.250.000 ₫`. */
export function formatMoney(amount: number): string {
  return moneyFormat.format(amount);
}

/** `(12.5, "Kilogram")` → `12,5 kg`. */
export function formatQuantity(value: number, unit: Unit): string {
  return `${quantityFormat.format(value)}${NBSP}${unitLabels[unit]}`;
}

/** Diện tích lưu bằng m²: `1500` → `1.500 m²`; báo cáo năng suất dùng `"ha"`: `0,15 ha`. */
export function formatArea(areaM2: number, unit: "m2" | "ha" = "m2"): string {
  if (unit === "ha") {
    return `${areaHaFormat.format(areaM2 / SQUARE_METERS_PER_HECTARE)}${NBSP}ha`;
  }
  return `${areaM2Format.format(areaM2)}${NBSP}m²`;
}

/**
 * Ngày nghiệp vụ backend gửi dạng `yyyy-MM-dd` → `dd/MM/yyyy`.
 * Tách chuỗi, không qua `Date`, để ngày không bị lệch theo múi giờ của máy.
 */
export function formatDate(date: string): string {
  const match = DATE_ONLY.exec(date);
  if (!match) {
    return date;
  }
  const [, year, month, day] = match;
  return `${day}/${month}/${year}`;
}

/** Thời điểm ISO 8601 UTC → `dd/MM/yyyy HH:mm` theo giờ Việt Nam, không phụ thuộc múi giờ của máy. */
export function formatDateTime(instant: string): string {
  const value = new Date(instant);
  if (Number.isNaN(value.getTime())) {
    return instant;
  }
  // vi-VN in giờ trước ngày ("14:30 26/09/2026"), nên ghép lại từng phần theo thứ tự quy ước.
  const parts = Object.fromEntries(dateTimeFormat.formatToParts(value).map((part) => [part.type, part.value]));
  return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}`;
}

/**
 * Đọc số người dùng gõ vào ô số thập phân: nhận cả `,` và `.` làm dấu thập phân.
 * Trả `null` khi ô trống hoặc không phải số; khoảng giá trị do schema của form kiểm tra.
 */
export function parseDecimalInput(text: string): number | null {
  const value = text.trim();
  if (!DECIMAL_INPUT.test(value)) {
    return null;
  }
  return Number(value.replace(",", "."));
}
