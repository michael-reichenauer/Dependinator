using System.Text.Json;
using Dependinator.Core.Claude;

namespace Dependinator.UI.Diagrams;

// Claude's verdict on a cycle, as the accept dialog shows it: accept it, with the reason to
// store, or break it, with the change that does. Assumptions are what Claude took for granted
// about code the prompt does not show, so the user knows what to check.
public record CycleSuggestion(bool IsAccept, string Why, string Assumptions, string Reason, string Change)
{
    // The fields of CyclePrompt's answer format; structured output makes Claude's JSON match it.
    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "verdict": { "type": "string", "enum": ["Accept", "Break"] },
            "why": { "type": "string", "description": "2–4 sentences." },
            "assumptions": { "type": "string", "description": "What was assumed about code that is not shown, or \"None\"." },
            "reasonToStore": { "type": "string", "description": "Only if Accept, else empty: at most 200 characters, specific to these members." },
            "change": { "type": "string", "description": "Only if Break, else empty: the smallest concrete change that removes one direction, and which direction." }
          },
          "required": ["verdict", "why", "assumptions", "reasonToStore", "change"],
          "additionalProperties": false
        }
        """;

    public static Result<CycleSuggestion> Parse(string json)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error("Claude's answer could not be read.", parsed.Error);
        return new CycleSuggestion(
            answer.Verdict == "Accept",
            answer.Why.Trim(),
            answer.Assumptions.Trim(),
            answer.ReasonToStore.Trim(),
            answer.Change.Trim()
        );
    }

    record Answer(string Verdict, string Why, string Assumptions, string ReasonToStore, string Change);
}

interface ICycleSuggestionService
{
    // Whether this host can ask Claude: the web host and VS Code, not the standalone browser app.
    bool IsAvailable { get; }

    Task<Result<CycleSuggestion>> SuggestAsync(Cycle cycle);
}

// Asks Claude about a cycle with the prompt the user can also copy (CyclePromptService).
[Scoped]
class CycleSuggestionService(ICyclePromptService cyclePromptService, IClaudeService claudeService)
    : ICycleSuggestionService
{
    public bool IsAvailable => Dependinator.Core.Build.IsWeb || Dependinator.Core.Build.IsVsCodeExtWasm;

    public async Task<Result<CycleSuggestion>> SuggestAsync(Cycle cycle)
    {
        // The e2e tests exercise the dialog without a key or network.
        if (Dependinator.Core.Build.IsTestMode)
            return TestModeSuggestion;

        var prompt = await cyclePromptService.GetPromptAsync(cycle);
        var answer = await claudeService.AskAsync(prompt, CycleSuggestion.JsonSchema);
        if (answer is not string json)
            return answer.Error;
        return CycleSuggestion.Parse(json);
    }

    static readonly CycleSuggestion TestModeSuggestion = new(
        true,
        "Test mode: Claude was not asked.",
        "None",
        "Suggested in test mode",
        ""
    );
}
