import {
  Building2Icon,
  ChartColumnIcon,
  CircleUserIcon,
  ClipboardCheckIcon,
  ContactIcon,
  EllipsisIcon,
  HandCoinsIcon,
  HistoryIcon,
  HouseIcon,
  LandPlotIcon,
  LeafIcon,
  ListChecksIcon,
  type LucideIcon,
  PackageIcon,
  PackagePlusIcon,
  PlusIcon,
  ReceiptIcon,
  ScrollTextIcon,
  SettingsIcon,
  SparklesIcon,
  SproutIcon,
  UsersIcon,
  WalletIcon,
  WarehouseIcon,
  WheatIcon,
} from "lucide-react";
import { ACCESS, type Access, canAccess, type UserAccessInfo, visibleTo } from "@/lib/auth/access";

// Nguồn duy nhất cho thanh tab, menu bên trái, trang "Thêm", bảng "＋". Module cần thêm mục thì báo người viết file này,
// không tự khai báo ở nơi khác. Mục của module chưa làm vẫn khai báo sẵn; trang chưa có thì hiện "Không tìm thấy".
// Một mục xuất hiện ở nhiều chỗ luôn dùng cùng một biểu tượng.

export interface NavItem {
  label: string;
  icon: LucideIcon;
  to: string;
  /** Ai thấy mục này; cùng điều kiện với route của trang để menu và trang không lệch nhau. */
  access: Access;
  /** Chỉ coi là đang chọn khi đường dẫn khớp đúng (trang "Hôm nay" là `/`). */
  end?: boolean;
}

export interface NavGroup {
  title: string;
  items: readonly NavItem[];
}

/** Ô `＋` giữa thanh tab, mở bảng ghi nhanh thay vì chuyển trang. */
export const QUICK_ADD = "quickAdd";

export type TabEntry = NavItem | typeof QUICK_ADD;

/** Nhãn, biểu tượng của nút mở bảng ghi nhanh: `＋` trên thanh tab, "Ghi nhanh" ở menu trái. */
export const quickAddButton = { label: "Ghi nhanh", icon: PlusIcon } as const;

/**
 * Thanh tab theo vai trò, trái sang phải. Chủ hộ, Thành viên hộ: Hôm nay, Công việc, ＋, Mùa vụ, Thêm.
 * Nhân công: Việc của tôi, Tài khoản. Quản trị hệ thống không có thanh tab.
 */
export const tabs: readonly TabEntry[] = [
  { label: "Hôm nay", icon: HouseIcon, to: "/", access: ACCESS.household, end: true },
  { label: "Công việc", icon: ListChecksIcon, to: "/tasks", access: ACCESS.household },
  QUICK_ADD,
  { label: "Mùa vụ", icon: SproutIcon, to: "/seasons", access: ACCESS.household },
  { label: "Thêm", icon: EllipsisIcon, to: "/more", access: ACCESS.household },
  { label: "Việc của tôi", icon: ListChecksIcon, to: "/tasks", access: ACCESS.worker },
  { label: "Tài khoản", icon: CircleUserIcon, to: "/account", access: ACCESS.worker },
];

/** Trang "Thêm" (`/more`), cũng hiện thẳng ra ở menu bên trái trên máy tính. Nhóm không còn mục nào thì ẩn. */
export const moreGroups: readonly NavGroup[] = [
  {
    title: "Sản xuất",
    items: [
      { label: "Thu hoạch", icon: WheatIcon, to: "/harvests", access: ACCESS.household },
      { label: "Tồn nông sản", icon: WarehouseIcon, to: "/produce-stock", access: ACCESS.household },
    ],
  },
  {
    title: "Kho",
    items: [
      { label: "Vật tư", icon: PackageIcon, to: "/supplies", access: ACCESS.household },
      { label: "Lịch sử kho", icon: HistoryIcon, to: "/stock-transactions", access: ACCESS.stockManager },
    ],
  },
  {
    title: "Tài chính",
    items: [
      { label: "Bán hàng", icon: HandCoinsIcon, to: "/sales", access: ACCESS.financeManager },
      { label: "Công nợ", icon: WalletIcon, to: "/debts", access: ACCESS.financeManager },
      { label: "Chi phí", icon: ReceiptIcon, to: "/expenses", access: ACCESS.financeManager },
      { label: "Khách hàng", icon: ContactIcon, to: "/customers", access: ACCESS.financeManager },
    ],
  },
  {
    title: "Báo cáo",
    items: [{ label: "Báo cáo", icon: ChartColumnIcon, to: "/reports", access: ACCESS.household }],
  },
  {
    title: "Trợ lý",
    items: [{ label: "Trợ lý AI", icon: SparklesIcon, to: "/ai", access: ACCESS.household }],
  },
  {
    title: "Danh mục",
    items: [
      { label: "Thửa đất", icon: LandPlotIcon, to: "/plots", access: ACCESS.household },
      { label: "Cây trồng", icon: LeafIcon, to: "/crops", access: ACCESS.household },
    ],
  },
  {
    title: "Quản lý hộ",
    items: [
      // end: không tô "Thông tin hộ" khi đang ở "/household/settings" (Cài đặt hộ).
      { label: "Thông tin hộ", icon: Building2Icon, to: "/household", access: ACCESS.household, end: true },
      { label: "Thành viên", icon: UsersIcon, to: "/members", access: ACCESS.owner },
      { label: "Cài đặt hộ", icon: SettingsIcon, to: "/household/settings", access: ACCESS.owner },
      { label: "Nhật ký thao tác", icon: ScrollTextIcon, to: "/audit-logs", access: ACCESS.owner },
    ],
  },
  {
    title: "Cá nhân",
    items: [{ label: "Tài khoản", icon: CircleUserIcon, to: "/account", access: ACCESS.everyone }],
  },
];

/** Bảng ghi nhanh "＋": mỗi nút một dòng, đi thẳng tới form. */
export const quickAddItems: readonly NavItem[] = [
  { label: "Ghi kết quả công việc", icon: ClipboardCheckIcon, to: "/tasks?mine=true", access: ACCESS.household },
  { label: "Thu hoạch", icon: WheatIcon, to: "/harvests/new", access: ACCESS.household },
  { label: "Bán hàng", icon: HandCoinsIcon, to: "/sales/new", access: ACCESS.financeManager },
  { label: "Nhập kho", icon: PackagePlusIcon, to: "/stock/in", access: ACCESS.stockManager },
  { label: "Ghi chi phí", icon: ReceiptIcon, to: "/expenses/new", access: ACCESS.financeManager },
];

/** Điều hướng của một người dùng: chỉ các mục được thấy, bỏ nhóm rỗng. */
export interface UserNavigation {
  tabs: readonly TabEntry[];
  /** Menu bên trái: các mục của thanh tab (bỏ `＋` và "Thêm"), rồi các nhóm của trang "Thêm" hiện thẳng ra. */
  sidebarItems: readonly NavItem[];
  groups: readonly NavGroup[];
  quickAddItems: readonly NavItem[];
}

export function navigationFor(user: UserAccessInfo): UserNavigation {
  const visibleQuickAdd = visibleTo(quickAddItems, user);
  const visibleTabs = tabs.filter((entry) =>
    entry === QUICK_ADD ? visibleQuickAdd.length > 0 : canAccess(user, entry.access),
  );
  const tabItems = visibleTabs.filter((entry): entry is NavItem => entry !== QUICK_ADD);
  const sidebarItems = tabItems.filter((item) => item.to !== "/more");

  // Mục đã có ở phần thanh tab thì không lặp lại trong nhóm (Nhân công có "Tài khoản" ở cả hai).
  const shownPaths = new Set(sidebarItems.map((item) => item.to));
  const sidebarGroups = moreGroupsFor(user)
    .map((group) => ({ ...group, items: group.items.filter((item) => !shownPaths.has(item.to)) }))
    .filter((group) => group.items.length > 0);

  return { tabs: visibleTabs, sidebarItems, groups: sidebarGroups, quickAddItems: visibleQuickAdd };
}

/** Các nhóm của trang "Thêm" người dùng được thấy, bỏ nhóm rỗng. */
export function moreGroupsFor(user: UserAccessInfo): NavGroup[] {
  return moreGroups
    .map((group) => ({ ...group, items: visibleTo(group.items, user) }))
    .filter((group) => group.items.length > 0);
}
