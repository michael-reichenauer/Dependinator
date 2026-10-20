using System.Text.Json;
using System.Text.RegularExpressions;
using Dependinator.Core.Claude;

namespace Dependinator.UI.Diagrams;

// Claude's verdict on a cycle, as the review dialog shows it: accept it, with the reason to
// store, or break it, with the change that does. Assumptions are what Claude took for granted
// about code the prompt does not show, so the user knows what to check. IsPasted: the answer of
// another AI chat to the copied prompt, pasted back by the user.
public partial record CycleSuggestion(bool IsAccept, string Why, string Assumptions, string Reason, string Change)
{
    public bool IsPasted { get; init; }

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

    // An AI chat's answer to the copied prompt, pasted back: the lines of the prompt's answer
    // format ("Verdict: Break", "Why: ..."), wherever they are in the text, markdown emphasis and
    // list marks aside. A field runs until the next one.
    public static Result<CycleSuggestion> ParseText(string text)
    {
        Dictionary<string, string> fields = [];
        var labels = FieldLabel().Matches(text);
        for (var i = 0; i < labels.Count; i++)
        {
            var start = labels[i].Index + labels[i].Length;
            var end = i + 1 < labels.Count ? labels[i + 1].Index : text.Length;
            var label = labels[i].Groups["label"].Value.ToLowerInvariant();
            fields.TryAdd(label.StartsWith("reason") ? "reason" : label, Clean(text[start..end]));
        }

        if (VerdictWord().Match(fields.GetValueOrDefault("verdict", "")) is not { Success: true } verdict)
            return new Error(
                "The answer has no \"Verdict: Accept\" or \"Verdict: Break\" line, so it could not be read."
            );
        var isAccept = verdict.Value.Equals("accept", StringComparison.OrdinalIgnoreCase);
        return new CycleSuggestion(
            isAccept,
            fields.GetValueOrDefault("why", ""),
            fields.GetValueOrDefault("assumptions", ""),
            isAccept ? fields.GetValueOrDefault("reason", "") : "",
            isAccept ? "" : fields.GetValueOrDefault("change", "")
        )
        {
            IsPasted = true,
        };
    }

    // A field's text without the format's "(only if Accept)" note, emphasis marks or quotes
    // around it; "N/A" or a dash for a field that does not apply is empty.
    static string Clean(string value)
    {
        var text = OnlyIfNote().Replace(value.Trim(), "").Trim().Trim('*', '_').Trim();
        if (text.Length > 1 && text[0] is '"' or '“' && text[^1] is '"' or '”')
            text = text[1..^1].Trim();
        return text is "N/A" or "n/a" or "-" or "—" or "–" ? "" : text;
    }

    [GeneratedRegex(
        @"^[ \t>*_#-]*(?<label>verdict|why|assumptions|reason to store|reason|change)[ \t*_]*:[ \t*_]*",
        RegexOptions.IgnoreCase | RegexOptions.Multiline
    )]
    private static partial Regex FieldLabel();

    [GeneratedRegex(@"\b(accept|break)\b", RegexOptions.IgnoreCase)]
    private static partial Regex VerdictWord();

    [GeneratedRegex(@"^\(only if (accept|break)\)", RegexOptions.IgnoreCase)]
    private static partial Regex OnlyIfNote();
}

// What the dialog says while a suggestion is on its way. In VS Code the key is entered in an
// input box at the top of the window, outside the UI the user is watching, so the dialog says so.
public enum SuggestStep
{
    EnteringApiKey,
    EnteringNewApiKey,
    AskingClaude,
}

interface ICycleSuggestionService
{
    // Whether this host can ask Claude: the web host and VS Code, not the standalone browser app.
    bool IsAvailable { get; }

    // Asks Claude about the cycle. Without a key, or with one Claude did not accept, the user is
    // asked for a key first (once per call); onStep tells the dialog what is going on.
    Task<Result<CycleSuggestion>> SuggestAsync(Cycle cycle, Action<SuggestStep> onStep);
}

// Asks Claude about a cycle with the prompt the user can also copy (CyclePromptService).
[Scoped]
class CycleSuggestionService(ICyclePromptService cyclePromptService, IClaudeService claudeService)
    : ICycleSuggestionService
{
    public bool IsAvailable => Dependinator.Core.Build.IsWeb || Dependinator.Core.Build.IsVsCodeExtWasm;

    public async Task<Result<CycleSuggestion>> SuggestAsync(Cycle cycle, Action<SuggestStep> onStep)
    {
        // The e2e tests exercise the dialog without a key or network: a larger cycle is to be
        // broken, a pair accepted and, asked again, broken, so differing answers can be tried too.
        // A short wait lets them see the dialog while it asks.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(SuggestStep.AskingClaude);
            await Task.Delay(TestModeAnswerDelay);
            return cycle.Members.Count == 2 && testModeAsks++ % 2 == 0 ? TestModeAccept : TestModeBreak;
        }

        var hasAskedForKey = false;
        string? prompt = null;
        while (true)
        {
            var state = await claudeService.GetApiKeyStateAsync();
            if (state is not ClaudeApiKeyState.Set)
            {
                onStep(state is ClaudeApiKeyState.Missing ? SuggestStep.EnteringApiKey : SuggestStep.EnteringNewApiKey);
                if (await claudeService.AskUserForApiKeyAsync() is Error e)
                    return e;
                hasAskedForKey = true;
            }

            onStep(SuggestStep.AskingClaude);
            prompt ??= await cyclePromptService.GetPromptAsync(cycle);
            var answer = await claudeService.AskAsync(prompt, CycleSuggestion.JsonSchema);
            if (answer is string json)
                return CycleSuggestion.Parse(json);
            // A key Claude did not accept: ask for a new one, once.
            if (hasAskedForKey || await claudeService.GetApiKeyStateAsync() is not ClaudeApiKeyState.Rejected)
                return answer.Error;
        }
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
