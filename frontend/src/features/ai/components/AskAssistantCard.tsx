import { SparklesIcon } from "lucide-react";
import { type FormEvent, useState } from "react";
import { useNavigate } from "react-router";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ASSISTANT_QUESTION_MAX_LENGTH, type AssistantLocationState } from "@/features/ai/types";

/**
 * Khối "Hỏi trợ lý" của trang Hôm nay (khối 5): nhập câu hỏi rồi mở `/ai`, trang trợ lý tự gửi câu hỏi đó.
 * Câu hỏi đi qua state của điều hướng, không nằm trên đường dẫn.
 */
export function AskAssistantCard() {
  const navigate = useNavigate();
  const [question, setQuestion] = useState("");

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const trimmed = question.trim();
    void navigate("/ai", trimmed ? { state: { question: trimmed } satisfies AssistantLocationState } : undefined);
  }

  return (
    <section className="flex flex-col gap-2 rounded-lg border bg-card p-3">
      <h2 className="flex items-center gap-2 text-sm font-medium text-muted-foreground">
        <SparklesIcon className="size-4" aria-hidden />
        Hỏi trợ lý
      </h2>
      <form className="flex gap-2" onSubmit={handleSubmit}>
        <Input
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          placeholder="Tháng này tốn bao nhiêu tiền phân bón?"
          aria-label="Câu hỏi cho trợ lý"
          maxLength={ASSISTANT_QUESTION_MAX_LENGTH}
          enterKeyHint="send"
          className="h-11 flex-1"
        />
        <Button type="submit" className="h-11">
          Hỏi
        </Button>
      </form>
    </section>
  );
}
