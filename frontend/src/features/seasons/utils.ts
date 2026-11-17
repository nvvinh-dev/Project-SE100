const deviationFormat = new Intl.NumberFormat("vi-VN", {
  maximumFractionDigits: 1,
  signDisplay: "exceptZero",
});

/** Định dạng độ lệch sản lượng theo chuẩn số Việt Nam, ví dụ `-60,5%`, `+12,5%`. */
export function formatYieldDeviation(value: number): string {
  return `${deviationFormat.format(value)}%`;
}

/** Phần trăm tiến độ thu hoạch so với mục tiêu (giới hạn `0..100`). */
export function computeYieldProgressPercent(harvested: number, target: number): number {
  if (target <= 0) {
    return 0;
  }
  return Math.min(100, Math.round((harvested / target) * 100));
}
