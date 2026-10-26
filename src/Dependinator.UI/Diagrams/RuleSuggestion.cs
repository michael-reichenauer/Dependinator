using System.Text.Json;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling.Models;
using static Dependinator.UI.Diagrams.PromptParts;

namespace Dependinator.UI.Diagrams;

// An AI's verdict on a dependency that breaks an architecture rule, as the review dialog shows it:
// fix it, with the change that removes it, or allow it, with the reason to store. Assumptions are
// what the AI took for granted about code the prompt does not show.
public record RuleSuggestion(bool IsFix, string Why, string Assumptions, string Reason, string Change) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    public string Verdict => IsFix ? "Fix" : "Allow";

    // The fields of RulePrompt's answer format; structured output makes the JSON match it.
    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "verdict": { "type": "string", "enum": ["Fix", "Allow"] },
            "why": { "type": "string", "description": "2–4 sentences." },
            "assumptions": { "type": "string", "description": "What was assumed about code that is not shown, or \"None\"." },
            "reasonToStore": { "type": "string", "description": "Only if Allow, else empty: at most 200 characters, specific to this dependency." },
            "change": { "type": "string", "description": "Only if Fix, else empty: the smallest concrete change that removes the dependency." }
          },
          "required": ["verdict", "why", "assumptions", "reasonToStore", "change"],
          "additionalProperties": false
        }
        """;

    public static Result<RuleSuggestion> Parse(string json, AiSource source = AiSource.Claude)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error($"{source.Name()}'s answer could not be read.", parsed.Error);
        var isFix = answer.Verdict == "Fix";
        return new RuleSuggestion(
            isFix,
            answer.Why.Trim(),
            answer.Assumptions.Trim(),
            isFix ? "" : answer.ReasonToStore.Trim(),
            isFix ? answer.Change.Trim() : ""
        )
        {
            Source = source,
        };
    }

    record Answer(string Verdict, string Why, string Assumptions, string ReasonToStore, string Change);

    static readonly string[] Labels = ["verdict", "why", "assumptions", "reason to store", "reason", "change"];

    // An AI chat's answer to the copied prompt, pasted back (see AiAnswerText).
    public static Result<RuleSuggestion> ParseText(string text)
    {
        var fields = AiAnswerText.Fields(text, Labels);
        if (AiAnswerText.Verdict(fields.GetValueOrDefault("verdict", ""), ["Fix", "Allow"]) is not { } verdict)
            return new Error("The answer has no \"Verdict: Fix\" or \"Verdict: Allow\" line, so it could not be read.");
        var isFix = verdict == "Fix";
        var reason = fields.GetValueOrDefault("reason to store") ?? fields.GetValueOrDefault("reason", "");
        return new RuleSuggestion(
            isFix,
            fields.GetValueOrDefault("why", ""),
            fields.GetValueOrDefault("assumptions", ""),
            isFix ? "" : reason,
            isFix ? fields.GetValueOrDefault("change", "") : ""
        )
        {
            Source = AiSource.Chat,
        };
    }
}

interface IRuleReviewService
{
    // Whether this host can ask Claude: the web host and VS Code, not the standalone browser app.
    bool IsAvailable { get; }

    // The text that asks an AI whether the dependency should be fixed or allowed (RulePrompt),
    // with the source of the type that makes it where the host can read the files.
    Task<string> GetPromptAsync(ArchitectureRule rule, RuleViolation violation);

    // The text that asks an AI that can change the code to remove the dependency as suggested.
    string GetFixPrompt(ArchitectureRule rule, RuleViolation violation, RuleSuggestion suggestion);

    // Asks Claude (or Claude Code) about the dependency, see IAiService.AskAsync.
    Task<Result<RuleSuggestion>> SuggestAsync(
        ArchitectureRule rule,
        RuleViolation violation,
        AiSource route,
        Action<SuggestStep> onStep
    );
}

[Scoped]
class RuleReviewService(IModelMgr modelMgr, IWorkspaceFileService workspaceFileService, IAiService aiService)
    : IRuleReviewService
{
    public bool IsAvailable => aiService.IsAvailable;

    public async Task<string> GetPromptAsync(ArchitectureRule rule, RuleViolation violation)
    {
        IReadOnlyList<SourceRequest> requests = [];
        if (CanReadSource)
        {
            using var model = modelMgr.UseModel();
            requests = RulePrompt.GetSourceRequests(model, violation);
        }

        // Read without holding the model, since in VS Code the files come from the LSP process.
        var sources = await ReadSourcesAsync(workspaceFileService, requests);

        using (var model = modelMgr.UseModel())
            return RulePrompt.Build(model, rule, violation, sources);
    }

    public string GetFixPrompt(ArchitectureRule rule, RuleViolation violation, RuleSuggestion suggestion)
    {
        using var model = modelMgr.UseModel();
        return RulePrompt.BuildFix(model, rule, violation, suggestion);
    }

    public async Task<Result<RuleSuggestion>> SuggestAsync(
        ArchitectureRule rule,
        RuleViolation violation,
        AiSource route,
        Action<SuggestStep> onStep
    )
    {
        // The e2e tests exercise the dialog without a key or network: the first answer says fix
        // it, the next allow it, so both verdicts and differing answers can be tried.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route is AiSource.ClaudeCode ? SuggestStep.AskingClaudeCode : SuggestStep.AskingClaude);
            await Task.Delay(TestModeAnswerDelay);
            var answer = testModeAsks++ % 2 == 0 ? TestModeFix : TestModeAllow;
            return answer with { Source = route };
        }

        var json = await aiService.AskAsync(
            route,
            () => GetPromptAsync(rule, violation),
            RuleSuggestion.JsonSchema,
            onStep
        );
        return json is string text ? RuleSuggestion.Parse(text, route) : json.Error;
    }

    int testModeAsks;
    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);

    static readonly RuleSuggestion TestModeFix = new(
        true,
        "Test mode: Claude was not asked.",
        "None",
        "",
        "Test mode: depend on an interface instead."
    );

    static readonly RuleSuggestion TestModeAllow = new(
        false,
        "Test mode: Claude was not asked.",
        "None",
        "Allowed in test mode",
        ""
    );
}
