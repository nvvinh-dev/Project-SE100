import { FileSpreadsheetIcon, FileTextIcon, Loader2Icon } from "lucide-react";
import { useState } from "react";
import { PermissionGate } from "@/components/common/PermissionGate";
import { showError, showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { downloadReportFile } from "@/features/reports/common/api";
import type { ReportFilter } from "@/features/reports/common/types";
import { type Access, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import type { ExportFormat } from "@/lib/enums";

interface ExportButtonsProps {
  /** Đường dẫn API xuất báo cáo (ví dụ `/reports/production/export`). */
  endpoint: string;
  /** Toàn bộ bộ lọc hiện tại gửi kèm cho backend lưu lịch sử report_exports. */
  filter: ReportFilter & Record<string, unknown>;
  /** Điều kiện quyền của báo cáo tương ứng (FR-RPT-02). */
  access: Access;
  disabled?: boolean;
  /** Bật nút "Xuất PDF" khi backend đã hỗ trợ định dạng PDF (FR-RPT-05). */
  enablePdf?: boolean;
}

/**
 * Cặp nút "Xuất Excel", "Xuất PDF" dùng chung cho các báo cáo (FR-RPT-04, FR-RPT-05).
 */
export function ExportButtons({
  endpoint,
  filter,
  access,
  disabled = false,
  enablePdf = false,
}: ExportButtonsProps) {
  const { data: user } = useCurrentUser();
  const [exportingFormat, setExportingFormat] = useState<ExportFormat | null>(null);
  const allowed = user !== undefined && canAccess(user, access);

  async function handleExport(format: ExportFormat) {
    setExportingFormat(format);
    try {
      await downloadReportFile(endpoint, { ...filter, format });
      showSuccess("Đã tải xuống tệp báo cáo.");
    } catch (error) {
      showError(error);
    } finally {
      setExportingFormat(null);
    }
  }

  const isBusy = exportingFormat !== null;

  return (
    <PermissionGate allowed={allowed}>
      <div className="flex flex-wrap items-center gap-2">
        <Button
          type="button"
          variant="outline"
          className="min-h-11 md:min-h-9"
          disabled={disabled || isBusy}
          onClick={() => void handleExport("Excel")}
        >
          {exportingFormat === "Excel" ? (
            <Loader2Icon className="animate-spin" data-icon="inline-start" />
          ) : (
            <FileSpreadsheetIcon data-icon="inline-start" />
          )}
          Xuất Excel
        </Button>

        {enablePdf && (
          <Button
            type="button"
            variant="outline"
            className="min-h-11 md:min-h-9"
            disabled={disabled || isBusy}
            onClick={() => void handleExport("Pdf")}
          >
            {exportingFormat === "Pdf" ? (
              <Loader2Icon className="animate-spin" data-icon="inline-start" />
            ) : (
              <FileTextIcon data-icon="inline-start" />
            )}
            Xuất PDF
          </Button>
        )}
      </div>
    </PermissionGate>
  );
}
