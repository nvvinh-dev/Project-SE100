import { MoneyText } from "@/components/common/MoneyText";
import { QuantityText } from "@/components/common/QuantityText";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { SupplyReportGroupItem, SupplyReportRow } from "@/features/reports/supplies/types";
import { supplyGroupLabels } from "@/lib/enum-labels";
import { cn } from "@/lib/utils";

function Quantity({ value, row }: { value: number; row: SupplyReportRow }) {
  return <QuantityText value={value} unit={row.unit} className={cn(value < 0 && "text-red-700")} />;
}

function SupplyName({ row }: { row: SupplyReportRow }) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <span className="font-medium">{row.name}</span>
      <span className="text-xs text-muted-foreground">{row.code}</span>
      {row.status === "Inactive" && <StatusBadge kind="RecordStatus" value="Inactive" />}
    </div>
  );
}

function GroupCards({ group }: { group: SupplyReportGroupItem }) {
  return (
    <ul className="flex flex-col gap-3 md:hidden">
      {group.supplies.map((row) => (
        <li key={row.supplyId} className="flex flex-col gap-2 rounded-lg border p-3">
          <SupplyName row={row} />
          <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
            <dt className="text-muted-foreground">Tồn đầu kỳ</dt>
            <dd className="text-right">
              <Quantity value={row.openingQuantity} row={row} />
            </dd>
            <dt className="text-muted-foreground">Nhập</dt>
            <dd className="text-right">
              <Quantity value={row.inQuantity} row={row} /> · <MoneyText amount={row.inValue} />
            </dd>
            <dt className="text-muted-foreground">Xuất</dt>
            <dd className="text-right">
              <Quantity value={row.outQuantity} row={row} /> · <MoneyText amount={row.outValue} />
            </dd>
            <dt className="text-muted-foreground">Điều chỉnh</dt>
            <dd className="text-right">
              <Quantity value={row.adjustmentQuantity} row={row} /> · <MoneyText amount={row.adjustmentValue} />
            </dd>
            <dt className="text-muted-foreground">Tồn cuối kỳ</dt>
            <dd className="text-right font-medium">
              <Quantity value={row.closingQuantity} row={row} />
            </dd>
            <dt className="text-muted-foreground">Tồn hiện tại</dt>
            <dd className="text-right">
              <Quantity value={row.stockQuantity} row={row} />
            </dd>
            <dt className="text-muted-foreground">Giá trị tồn</dt>
            <dd className="text-right font-medium">
              <MoneyText amount={row.stockValue} />
            </dd>
          </dl>
        </li>
      ))}
      <li className="flex flex-col gap-1 rounded-lg bg-muted/50 p-3 text-sm">
        <span className="font-medium">Tổng nhóm (giá trị)</span>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Nhập · Xuất</span>
          <span>
            <MoneyText amount={group.inValue} /> · <MoneyText amount={group.outValue} />
          </span>
        </div>
        <div className="flex justify-between">
          <span className="text-muted-foreground">Giá trị tồn</span>
          <MoneyText amount={group.stockValue} className="font-semibold" />
        </div>
      </li>
    </ul>
  );
}

function GroupTable({ group }: { group: SupplyReportGroupItem }) {
  return (
    <div className="hidden md:block">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Vật tư</TableHead>
            <TableHead className="text-right">Tồn đầu kỳ</TableHead>
            <TableHead className="text-right">Nhập</TableHead>
            <TableHead className="text-right">Xuất</TableHead>
            <TableHead className="text-right">Điều chỉnh</TableHead>
            <TableHead className="text-right">Tồn cuối kỳ</TableHead>
            <TableHead className="text-right">Tồn hiện tại</TableHead>
            <TableHead className="text-right">Giá trị tồn</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {group.supplies.map((row) => (
            <TableRow key={row.supplyId}>
              <TableCell>
                <SupplyName row={row} />
              </TableCell>
              <TableCell className="text-right">
                <Quantity value={row.openingQuantity} row={row} />
              </TableCell>
              <TableCell className="text-right">
                <div className="flex flex-col items-end">
                  <Quantity value={row.inQuantity} row={row} />
                  <MoneyText amount={row.inValue} className="text-xs text-muted-foreground" />
                </div>
              </TableCell>
              <TableCell className="text-right">
                <div className="flex flex-col items-end">
                  <Quantity value={row.outQuantity} row={row} />
                  <MoneyText amount={row.outValue} className="text-xs text-muted-foreground" />
                </div>
              </TableCell>
              <TableCell className="text-right">
                <div className="flex flex-col items-end">
                  <Quantity value={row.adjustmentQuantity} row={row} />
                  <MoneyText amount={row.adjustmentValue} className="text-xs text-muted-foreground" />
                </div>
              </TableCell>
              <TableCell className="text-right font-medium">
                <Quantity value={row.closingQuantity} row={row} />
              </TableCell>
              <TableCell className="text-right">
                <Quantity value={row.stockQuantity} row={row} />
              </TableCell>
              <TableCell className="text-right font-medium">
                <MoneyText amount={row.stockValue} />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
        <TableFooter>
          <TableRow>
            <TableCell className="font-medium">Tổng nhóm (giá trị)</TableCell>
            <TableCell />
            <TableCell className="text-right">
              <MoneyText amount={group.inValue} />
            </TableCell>
            <TableCell className="text-right">
              <MoneyText amount={group.outValue} />
            </TableCell>
            <TableCell className="text-right">
              <MoneyText amount={group.adjustmentValue} />
            </TableCell>
            <TableCell />
            <TableCell />
            <TableCell className="text-right font-semibold">
              <MoneyText amount={group.stockValue} />
            </TableCell>
          </TableRow>
        </TableFooter>
      </Table>
    </div>
  );
}

/** Nhập, xuất, tồn theo nhóm vật tư (FR-RPT-SUP-01, BR-RPT-05): lượng theo từng vật tư, dòng tổng nhóm chỉ có giá trị. */
export function SupplyStockGroups({ groups }: { groups: readonly SupplyReportGroupItem[] }) {
  if (groups.length === 0) {
    return <p className="text-sm text-muted-foreground">Không có vật tư nào khớp bộ lọc.</p>;
  }

  return (
    <div className="flex flex-col gap-5">
      {groups.map((group) => (
        <div key={group.group} className="flex flex-col gap-2">
          <h3 className="text-sm font-semibold">{supplyGroupLabels[group.group]}</h3>
          <GroupCards group={group} />
          <GroupTable group={group} />
        </div>
      ))}
    </div>
  );
}
