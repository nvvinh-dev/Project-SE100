import { salesChannelLabels } from "@/lib/enum-labels";
import type { SalesChannel } from "@/lib/enums";

interface SaleBuyerInfo {
  customerName: string | null;
  channel: SalesChannel | null;
  channelOther: string | null;
}

/** Người mua hiển thị: khách hàng, không có thì kênh bán ("Khác · ghi chú" khi kênh Khác có ghi chú). */
export function buyerLabel(sale: SaleBuyerInfo): string {
  if (sale.customerName) {
    return sale.channel ? `${sale.customerName} · ${channelLabel(sale)}` : sale.customerName;
  }
  return sale.channel ? channelLabel(sale) : "—";
}

export function channelLabel(sale: SaleBuyerInfo): string {
  if (!sale.channel) {
    return "—";
  }
  const label = salesChannelLabels[sale.channel];
  return sale.channel === "Other" && sale.channelOther ? `${label} · ${sale.channelOther}` : label;
}

/** "Cây · Thửa" của mùa vụ. */
export function seasonShortLabel(sale: { cropName: string; plotCode: string }): string {
  return `${sale.cropName} · ${sale.plotCode}`;
}
