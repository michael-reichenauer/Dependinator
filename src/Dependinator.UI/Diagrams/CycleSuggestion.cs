using System.Text.Json;
using Dependinator.UI.Diagrams.Ai;

namespace Dependinator.UI.Diagrams;

// Claude's verdict on a cycle, as the review dialog shows it: accept it, with the reason to
// store, or break it, with the change that does. Assumptions are what Claude took for granted
// about code the prompt does not show, so the user knows what to check. Source: Claude, Claude
// Code, or another AI chat whose answer to the copied prompt the user pasted back.
public record CycleSuggestion(bool IsAccept, string Why, string Assumptions, string Reason, string Change) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    public bool IsPasted => Source == AiSource.Chat;

    public string Verdict => IsAccept ? "Accept" : "Break";

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

    public static Result<CycleSuggestion> Parse(string json, AiSource source = AiSource.Claude)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error($"{source.Name()}'s answer could not be read.", parsed.Error);
        if (answer.Verdict is not ("Accept" or "Break"))
            return new Error($"{source.Name()}'s answer has no verdict.");
        return new CycleSuggestion(
            answer.Verdict == "Accept",
            (answer.Why ?? "").Trim(),
            (answer.Assumptions ?? "").Trim(),
            (answer.ReasonToStore ?? "").Trim(),
            (answer.Change ?? "").Trim()
        )
        {
            Source = source,
        };
    }

    // Fields may be missing from an answer that did not come through structured output.
    record Answer(string? Verdict, string? Why, string? Assumptions, string? ReasonToStore, string? Change);

    static readonly string[] Labels = ["verdict", "why", "assumptions", "reason to store", "reason", "change"];

    // An AI chat's answer to the copied prompt, pasted back: the lines of the prompt's answer
    // format ("Verdict: Break", "Why: ..."), see AiAnswerText.
    public static Result<CycleSuggestion> ParseText(string text)
    {
        var fields = AiAnswerText.Fields(text, Labels);
        if (AiAnswerText.Verdict(fields.GetValueOrDefault("verdict", ""), ["Accept", "Break"]) is not { } verdict)
            return new Error(
                "The answer has no \"Verdict: Accept\" or \"Verdict: Break\" line, so it could not be read."
            );
        var isAccept = verdict == "Accept";
        var reason = fields.GetValueOrDefault("reason to store") ?? fields.GetValueOrDefault("reason", "");
        return new CycleSuggestion(
            isAccept,
            fields.GetValueOrDefault("why", ""),
            fields.GetValueOrDefault("assumptions", ""),
            isAccept ? reason : "",
            isAccept ? "" : fields.GetValueOrDefault("change", "")
        )
        {
            Source = AiSource.Chat,
        };
    }
}

interface ICycleSuggestionService
{
    // Whether this host can ask Claude: the web host and VS Code, not the standalone browser app.
    bool IsAvailable { get; }

    // Asks Claude (or Claude Code) about the cycle, see IAiService.AskAsync.
    Task<Result<CycleSuggestion>> SuggestAsync(Cycle cycle, AiSource route, Action<SuggestStep> onStep);
}

// Asks Claude about a cycle with the prompt the user can also copy (CyclePromptService).
[Scoped]
class CycleSuggestionService(ICyclePromptService cyclePromptService, IAiService aiService) : ICycleSuggestionService
{
    public bool IsAvailable => aiService.IsAvailable;

    public async Task<Result<CycleSuggestion>> SuggestAsync(Cycle cycle, AiSource route, Action<SuggestStep> onStep)
    {
        // The e2e tests exercise the dialog without a key or network: a larger cycle is to be
        // broken, a pair accepted and, asked again, broken, so differing answers can be tried too.
        // A short wait lets them see the dialog while it asks.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route is AiSource.ClaudeCode ? SuggestStep.AskingClaudeCode : SuggestStep.AskingClaude);
            await Task.Delay(TestModeAnswerDelay);
            var answer = cycle.Members.Count == 2 && testModeAsks++ % 2 == 0 ? TestModeAccept : TestModeBreak;
            return answer with { Source = route };
        }

        var json = await aiService.AskAsync(
            route,
            () => cyclePromptService.GetPromptAsync(cycle),
            CycleSuggestion.JsonSchema,
            onStep
        );
        return json is string text ? CycleSuggestion.Parse(text, route) : json.Error;
    }

    int testModeAsks;
    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);

    static readonly CycleSuggestion TestModeAccept = new(
        true,
        "Test mode: Claude was not asked.",
        "None",
        "Suggested in test mode",
        ""
    );

    static readonly CycleSuggestion TestModeBreak = new(
        false,
        "Test mode: Claude was not asked.",
        "None",
        "",
        "Test mode: move one member out of the cycle."
    );
}
