namespace Dependinator.UI.Diagrams.Ai;

// A question AiOpinion offers under an answer it is for (IsFor), such as "Ask how to break it"
// under an answer that suggests accepting a cycle: asked on any route like the first question,
// with its own prompt (GetPrompt, for the AI chat route and "Show prompt"), its answer joining
// the others. TestId is the suffix of its test ids ("accept-cycle-break-ask").
public sealed record AiFollowUp<TAnswer>(
    string Label,
    string Title,
    string TestId,
    Func<TAnswer, bool> IsFor,
    Func<TAnswer, Task<string>> GetPrompt,
    Func<TAnswer, AiSource, Action<SuggestStep>, Task<Result<TAnswer>>> Ask
)
    where TAnswer : class, IAiAnswer;
