import type { ReactNode } from "react";

interface ReportSectionProps {
  title: string;
  description?: string;
  children: ReactNode;
}

/** Một khối của báo cáo tài chính: tiêu đề, dòng mô tả ngắn và nội dung. */
export function ReportSection({ title, description, children }: ReportSectionProps) {
  return (
    <section className="flex flex-col gap-3 rounded-lg border bg-card p-4">
      <div>
        <h2 className="text-base font-semibold">{title}</h2>
        {description && <p className="mt-0.5 text-sm text-muted-foreground">{description}</p>}
      </div>
      {children}
    </section>
  );
}
