import { SparklesIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";

/** Nhãn bắt buộc của mọi nội dung do AI tạo, kèm dòng nhắc kiểm tra lại. */
export function AIGeneratedLabel() {
  return (
    <div className="flex flex-col gap-1">
      <Badge variant="secondary" className="w-fit">
        <SparklesIcon data-icon="inline-start" aria-hidden />
        Do AI tạo
      </Badge>
      <p className="text-xs text-muted-foreground">Nội dung chỉ dựa trên dữ liệu đã ghi nhận, hãy kiểm tra lại.</p>
    </div>
  );
}
