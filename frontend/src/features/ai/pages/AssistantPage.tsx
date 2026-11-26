import { Loader2Icon, SendIcon } from "lucide-react";
import { type FormEvent, useEffect, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { PageHeader } from "@/components/common/PageHeader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { getAIBlock } from "@/features/ai/ai-availability";
import { AIBlockedAlert } from "@/features/ai/components/AIBlockedAlert";
import { AIGeneratedLabel } from "@/features/ai/components/AIGeneratedLabel";
import { AIQuotaText } from "@/features/ai/components/AIQuotaText";
import { useAIQuota, useAskAssistant } from "@/features/ai/queries";
import {
  ASSISTANT_QUESTION_MAX_LENGTH,
  type AssistantExchange,
  type AssistantLocationState,
} from "@/features/ai/types";

// Máy chủ nhận tối đa chừng này lượt hỏi đáp trước; giữ vài lượt gần nhất là đủ để hỏi tiếp.
const HISTORY_MAX_COUNT = 10;

const SUGGESTIONS = ["Tháng này tốn bao nhiêu tiền phân bón?", "Thửa nào có mùa vụ trễ thu?", "Còn công việc nào trễ hạn?"];

/**
 * `/ai`: trợ lý hỏi đáp dữ liệu của hộ (FR-AI-02). Hội thoại chỉ giữ trên trang, tải lại là mất.
 * Hết lượt hoặc dịch vụ AI lỗi thì khóa ô nhập và hiện lý do (FR-AI-05).
 */
export function AssistantPage() {
  const location = useLocation();
  const navigate = useNavigate();
  const quota = useAIQuota();
  const ask = useAskAssistant();
  const [exchanges, setExchanges] = useState<AssistantExchange[]>([]);
  const [draft, setDraft] = useState("");
  const endRef = useRef<HTMLDivElement>(null);
  const initialQuestionSent = useRef(false);

  const block = getAIBlock(quota.data, ask.error);
  const otherError = ask.isError && block === null ? ask.error : null;
  const pendingQuestion = ask.isPending ? ask.variables.question : null;
  const locked = block !== null || ask.isPending;

  function send(text: string) {
    const question = text.trim();
    if (!question || locked) {
      return;
    }

    ask.mutate(
      { question, history: exchanges.slice(-HISTORY_MAX_COUNT) },
      {
        onSuccess: (response) => setExchanges((previous) => [...previous, { question, answer: response.answer }]),
        // Gửi lỗi thì trả câu hỏi về ô nhập để không phải gõ lại.
        onError: () => setDraft(question),
      },
    );
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!locked && draft.trim()) {
      send(draft);
      setDraft("");
    }
  }

  // Mở từ trang Hôm nay kèm câu hỏi: gửi luôn một lần, rồi xóa state để quay lại trang không gửi lại.
  const initialQuestion = (location.state as Partial<AssistantLocationState> | null)?.question;
  useEffect(() => {
    if (initialQuestionSent.current || typeof initialQuestion !== "string") {
      return;
    }
    initialQuestionSent.current = true;
    void navigate(location.pathname, { replace: true, state: null });
    send(initialQuestion);
    // Chỉ chạy khi trang mở lần đầu.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "end" });
  }, [exchanges.length, pendingQuestion]);

  return (
    <div className="flex min-h-[calc(100dvh-2rem)] flex-col gap-4 md:min-h-[calc(100dvh-3rem)]">
      <PageHeader title="Trợ lý" backTo="/more" />

      <div className="flex flex-col gap-1">
        <p className="text-sm">Hỏi về dữ liệu của hộ. Trợ lý không chẩn đoán bệnh cây, không dự báo.</p>
        <AIQuotaText />
      </div>

      {exchanges.length === 0 && pendingQuestion === null && (
        <div className="flex flex-col gap-2">
          <p className="text-sm text-muted-foreground">Gợi ý:</p>
          <div className="flex flex-wrap gap-2">
            {SUGGESTIONS.map((suggestion) => (
              <Button
                key={suggestion}
                type="button"
                variant="outline"
                className="h-auto min-h-11 text-left whitespace-normal"
                disabled={locked}
                onClick={() => send(suggestion)}
              >
                {suggestion}
              </Button>
            ))}
          </div>
        </div>
      )}

      <ol className="flex flex-col gap-4" aria-label="Hội thoại" aria-live="polite">
        {exchanges.map((exchange, index) => (
          <li key={index} className="flex flex-col gap-3">
            <QuestionBubble text={exchange.question} />
            <AnswerBlock text={exchange.answer} />
          </li>
        ))}
        {pendingQuestion !== null && (
          <li className="flex flex-col gap-3">
            <QuestionBubble text={pendingQuestion} />
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2Icon className="size-4 animate-spin" aria-hidden />
              Trợ lý đang trả lời...
            </p>
          </li>
        )}
      </ol>
      <div ref={endRef} />

      <div className="sticky bottom-0 -mx-4 mt-auto flex flex-col gap-2 border-t bg-background p-3 pb-[calc(0.75rem+env(safe-area-inset-bottom))] md:mx-0 md:rounded-lg md:border">
        {block && <AIBlockedAlert block={block} onRetry={() => ask.reset()} />}
        {otherError !== null && <ErrorAlert error={otherError} />}
        <form className="flex gap-2" onSubmit={handleSubmit}>
          <Input
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder="Nhập câu hỏi..."
            aria-label="Câu hỏi cho trợ lý"
            maxLength={ASSISTANT_QUESTION_MAX_LENGTH}
            enterKeyHint="send"
            disabled={block !== null}
            className="h-11 flex-1"
          />
          <Button type="submit" className="h-11" disabled={locked || !draft.trim()}>
            {ask.isPending ? (
              <Loader2Icon data-icon="inline-start" className="animate-spin" />
            ) : (
              <SendIcon data-icon="inline-start" />
            )}
            Gửi
          </Button>
        </form>
      </div>
    </div>
  );
}

function QuestionBubble({ text }: { text: string }) {
  return (
    <p className="ml-auto max-w-[85%] rounded-lg bg-primary px-3 py-2 break-words whitespace-pre-wrap text-primary-foreground">
      {text}
    </p>
  );
}

function AnswerBlock({ text }: { text: string }) {
  return (
    <div className="mr-auto flex max-w-[95%] flex-col gap-2">
      <AIGeneratedLabel />
      <p className="break-words whitespace-pre-wrap">{text}</p>
    </div>
  );
}
