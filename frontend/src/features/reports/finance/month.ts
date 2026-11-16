/** Tháng dạng `yyyy-MM` từ API sang `MM/yyyy` để hiển thị. */
export function formatMonth(month: string) {
  return `${month.slice(5, 7)}/${month.slice(0, 4)}`;
}

const shortNumber = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 1 });

/** Tiền rút gọn trên trục, nhãn biểu đồ: `1500000` → `1,5 tr`, `2300000000` → `2,3 tỷ` (`06-ui-screens` mục 8). */
export function formatMoneyShort(amount: number) {
  const abs = Math.abs(amount);
  if (abs >= 1_000_000_000) {
    return `${shortNumber.format(amount / 1_000_000_000)} tỷ`;
  }
  if (abs >= 1_000_000) {
    return `${shortNumber.format(amount / 1_000_000)} tr`;
  }
  if (abs >= 1_000) {
    return `${shortNumber.format(amount / 1_000)} ng`;
  }
  return shortNumber.format(amount);
}
