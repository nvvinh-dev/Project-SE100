using FluentValidation;

namespace HouseholdFarm.Api.Features.Ai;

public sealed class AskAssistantRequestValidator : AbstractValidator<AskAssistantRequest>
{
    public const int QuestionMaxLength = 500;

    // Lịch sử dài làm mỗi lượt tốn thêm nhiều token; giữ vài lượt gần nhất là đủ để hỏi tiếp.
    public const int HistoryMaxCount = 10;

    public const int AnswerMaxLength = 4000;

    public AskAssistantRequestValidator()
    {
        RuleFor(x => x.Question)
            .Must(q => !string.IsNullOrWhiteSpace(q)).WithMessage("Nhập câu hỏi.")
            .MaximumLength(QuestionMaxLength).WithMessage($"Câu hỏi tối đa {QuestionMaxLength} ký tự.");

        RuleFor(x => x.History)
            .Must(h => h is null || h.Count <= HistoryMaxCount)
            .WithMessage($"Chỉ gửi tối đa {HistoryMaxCount} lượt hỏi đáp trước.");

        RuleForEach(x => x.History).ChildRules(exchange =>
        {
            exchange.RuleFor(e => e.Question)
                .NotEmpty().WithMessage("Lượt hỏi đáp trước thiếu câu hỏi.")
                .MaximumLength(QuestionMaxLength).WithMessage($"Câu hỏi tối đa {QuestionMaxLength} ký tự.");
            exchange.RuleFor(e => e.Answer)
                .NotEmpty().WithMessage("Lượt hỏi đáp trước thiếu câu trả lời.")
                .MaximumLength(AnswerMaxLength).WithMessage($"Câu trả lời tối đa {AnswerMaxLength} ký tự.");
        });
    }
}
