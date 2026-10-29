using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dependinator.Core.ModelQueries;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using MudBlazor;
using static Dependinator.UI.Diagrams.PromptParts;

namespace Dependinator.UI.Diagrams;

// A rule an AI suggests, and what Dependinator itself found when it checked the rule on the model
// (the AI's own counts are not trusted): the nodes it names, and what it would break today.
public record SuggestedRule(string From, string To, string Why)
{
    public RuleCheck Check { get; init; } = RuleCheck.Unchecked;

    public bool CanAdd => Check.Error is null && !Check.IsRule;
}

// FromName and ToName are node names, the titles what the panel shows; Error says why the rule
// cannot be added (a node not found, one inside the other).
public record RuleCheck(
    string? Error,
    string FromName = "",
    string ToName = "",
    string FromTitle = "",
    string ToTitle = "",
    int ViolationCount = 0,
    int LinkCount = 0,
    bool IsRule = false
)
{
    public static readonly RuleCheck Unchecked = new("Not checked");
}

// An AI's suggested rules, as the Suggest rules dialog shows them.
public record RuleSuggestions(IReadOnlyList<SuggestedRule> Rules) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    public string Verdict => Rules.Count == 1 ? "1 rule" : $"{Rules.Count} rules";

    // The fields of SuggestRulesPrompt's answer format; structured output makes the JSON match it.
    public const string JsonSchema = """
        {
          "type": "object",
          "properties": {
            "rules": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "from": { "type": "string", "description": "The name of the node that must not depend on the other, as listed." },
                  "to": { "type": "string", "description": "The name of the node it must not depend on, as listed." },
                  "why": { "type": "string", "description": "1–2 sentences: what the rule protects." }
                },
                "required": ["from", "to", "why"],
                "additionalProperties": false
              }
            }
          },
          "required": ["rules"],
          "additionalProperties": false
        }
        """;

    public static Result<RuleSuggestions> Parse(string json, AiSource source = AiSource.Claude)
    {
        var parsed = Result.Catch(() => Json.Deserialize<Answer>(json) ?? throw new JsonException("No answer"));
        if (parsed is not Answer answer)
            return new Error($"{source.Name()}'s answer could not be read.", parsed.Error);
        List<SuggestedRule> rules =
        [
            .. (answer.Rules ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r.From) && !string.IsNullOrWhiteSpace(r.To))
                .Select(r => new SuggestedRule(Name(r.From!), Name(r.To!), (r.Why ?? "").Trim())),
        ];
        return rules.Count == 0
            ? new Error($"{source.Name()} suggested no rules.")
            : new RuleSuggestions(rules) { Source = source };
    }

    record Answer(List<AnswerRule>? Rules);

    record AnswerRule(string? From, string? To, string? Why);

    // "Rule: `A` must not use `B`", markdown emphasis, list marks and quotes aside.
    static readonly Regex RuleLine = new(
        @"^[\s>*_#\-\d.)]*Rule\s*\d*[*_]*\s*:[*_]*\s*(?<from>.+?)\s+must\s+not\s+(?:use|depend\s+on)\s+(?<to>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline
    );

    static readonly Regex WhyLine = new(@"^[\s>*_\-]*Why[*_]*\s*:[*_]*\s*(?<why>.*)$", RegexOptions.IgnoreCase);

    // An AI chat's answer to the copied prompt, pasted back: each "Rule:" line, and the "Why:"
    // that follows it.
    public static Result<RuleSuggestions> ParseText(string text)
    {
        List<SuggestedRule> rules = [];
        var lines = text.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (RuleLine.Match(lines[i]) is not { Success: true } rule)
                continue;
            var why = "";
            for (var j = i + 1; j < lines.Length && !RuleLine.IsMatch(lines[j]); j++)
            {
                if (WhyLine.Match(lines[j]) is { Success: true } whyLine)
                {
                    why = whyLine.Groups["why"].Value.Trim().Trim('*', '_').Trim();
                    break;
                }
            }
            rules.Add(new SuggestedRule(Name(rule.Groups["from"].Value), Name(rule.Groups["to"].Value), why));
        }
        return rules.Count == 0
            ? new Error("The answer has no \"Rule: A must not use B\" lines, so it could not be read.")
            : new RuleSuggestions(rules) { Source = AiSource.Chat };
    }

    static string Name(string text) => text.Trim().Trim('`', '*', '_', '"', '\'', '.', ',', ';').Trim();
}

// The prompt that asks an AI to suggest architecture rules: the model's structure (containers
// down to a depth that fits), the dependencies between the containers listed deepest, and the
// rules there are. The AI names nodes as listed; Dependinator then checks each rule itself.
static class SuggestRulesPrompt
{
    internal const int MaxSuggestions = 8;
    internal const int MaxListedNodes = 120;
    internal const int MaxDependencies = 150;

    public static string Build(IModel model)
    {
        var (listed, leftOut) = ListContainers(model);
        if (listed.Count == 0)
            return "";
        var typeCounts = TypeCounts(model);

        var text = new StringBuilder();
        text.AppendLine("# Suggest architecture rules").AppendLine();
        text.AppendLine(
            "Dependinator shows a codebase as a dependency model: containers (projects, namespaces, modules), the "
                + "types in them, and the links between them. An architecture rule says \"A must not use B\": nothing "
                + "at or inside node A may depend on anything at or inside node B. Dependinator counts the "
                + "dependencies that break each rule and draws them in the diagram, so a rule guards the "
                + "architecture as the code changes."
        );

        text.AppendLine().AppendLine($"## The structure of {DisplayPath(model, model.Path)}").AppendLine();
        text.AppendLine("Containers, with the number of types inside each; deeper levels are left out.").AppendLine();
        var depth = listed.ToDictionary(n => n, n => n.Ancestors().Count(a => !a.IsRoot));
        foreach (var node in listed)
        {
            var indent = new string(' ', depth[node] * 2);
            var types = typeCounts[node];
            text.AppendLine(
                $"{indent}- `{node.Name}` ({Kind(node.Type)}, {types} {(types == 1 ? "type" : "types")}){Titled(node)}{Described(node)}"
            );
        }

        if (leftOut > 0)
            text.AppendLine($"- … and {leftOut} more top-level containers, left out with their dependencies");

        AppendDependencies(text, listed);
        AppendRules(text, model);
        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    static readonly string TaskSection = $"""
        ## Your task

        Suggest up to {MaxSuggestions} rules worth keeping for this codebase: rules that state its intended layering or boundaries (which part may use which, or which part stays free of a technology), not rules that merely happen to hold. Prefer rules the code keeps today, or breaks with only a few dependencies; suggest a rule the code breaks widely only when the structure clearly intends it, and say so. Leave out the rules there are already, and pairs where one node contains the other. Name each node exactly as listed (the name in backticks). If you have Dependinator's tools, check each rule with check_rule before you suggest it.

        Answer in this format, for each rule:

        Rule: <name> must not use <name>
        Why: 1–2 sentences on what the rule protects.

        """;

    // The containers, level by level, as many whole levels as fit (at least the first, cut at the
    // limit), each right after its parent so the indented list reads as a tree.
    internal static List<Node> ListedNodes(IModel model) => ListContainers(model).Listed;

    static (List<Node> Listed, int LeftOut) ListContainers(IModel model)
    {
        HashSet<Node> included = [];
        var level = Containers(model.Root).ToList();
        var leftOut = Math.Max(0, level.Count - MaxListedNodes);
        while (level.Count > 0)
        {
            if (included.Count > 0 && included.Count + level.Count > MaxListedNodes)
                break;
            included.UnionWith(level.Take(MaxListedNodes));
            level = [.. level.Take(MaxListedNodes).SelectMany(Containers)];
        }

        List<Node> listed = [];
        void Walk(Node parent)
        {
            foreach (var child in Containers(parent).Where(included.Contains))
            {
                listed.Add(child);
                Walk(child);
            }
        }
        Walk(model.Root);
        return (listed, leftOut);
    }

    static IEnumerable<Node> Containers(Node node) =>
        node
            .Children.Where(c =>
                !c.Type.IsType && !c.Type.IsMember && !NodeViewPolicy.IsAnnotation(c) && c.Children.Count > 0
            )
            .OrderBy(c => c.Name, StringComparer.Ordinal);

    // The number of types at or inside each node, in one pass over the model down to the types
    // (members hold none).
    static Dictionary<Node, int> TypeCounts(IModel model)
    {
        Dictionary<Node, int> counts = [];
        int Count(Node node)
        {
            var count = node.Type.IsType ? 1 : 0;
            foreach (var child in node.Children.Where(c => !c.Type.IsMember))
                count += Count(child);
            counts[node] = count;
            return count;
        }
        Count(model.Root);
        return counts;
    }

    static string Titled(Node node) => node.LongName != node.Name ? $", titled \"{node.LongName}\"" : "";

    // Links counted between the listed containers they are deepest in, where neither contains
    // the other; most links first.
    static void AppendDependencies(StringBuilder text, List<Node> listed)
    {
        var included = listed.ToHashSet();
        Node? ListedAt(Node node)
        {
            for (var n = node; n is not null && !n.IsRoot; n = n.Parent)
            {
                if (included.Contains(n))
                    return n;
            }
            return null;
        }

        var counts = new Dictionary<(Node, Node), int>();
        foreach (var source in listed)
        {
            foreach (var link in source.Children.Where(c => !included.Contains(c)).SelectMany(Links))
            {
                if (
                    ListedAt(link.Target) is not { } target
                    || target.IsAtOrInside(source)
                    || source.IsAtOrInside(target)
                )
                    continue;
                counts[(source, target)] = counts.GetValueOrDefault((source, target)) + 1;
            }
        }

        text.AppendLine().AppendLine("## Dependencies between them").AppendLine();
        if (counts.Count == 0)
        {
            text.AppendLine("None.");
            return;
        }
        text.AppendLine("`A` → `B`: the number of links from code at or inside A to code at or inside B.").AppendLine();
        var shown = counts
            .OrderByDescending(c => c.Value)
            .ThenBy(c => c.Key.Item1.Name, StringComparer.Ordinal)
            .ThenBy(c => c.Key.Item2.Name, StringComparer.Ordinal)
            .Take(MaxDependencies);
        foreach (var ((source, target), count) in shown)
            text.AppendLine($"- `{source.Name}` → `{target.Name}`: {count}");
        if (counts.Count > MaxDependencies)
            text.AppendLine($"- … and {counts.Count - MaxDependencies} more with fewer links");
    }

    // The links of the code in a node that is not listed itself (its own, and those inside it).
    static IEnumerable<Link> Links(Node node) => node.DescendantsAndSelfPreOrder().SelectMany(n => n.SourceLinks);

    static void AppendRules(StringBuilder text, IModel model)
    {
        if (model.Rules.Count == 0)
            return;
        text.AppendLine().AppendLine("## The rules there are").AppendLine();
        foreach (var rule in model.Rules)
            text.AppendLine($"- `{rule.FromName}` must not use `{rule.ToName}`");
    }
}

// "Suggest rules" in the Rules panel: an AI (Claude, Claude Code with Dependinator's MCP tools, or
// the user's AI chat) suggests architecture rules from the model's structure and dependencies,
// Dependinator checks each one on the model (SuggestedRule.Check), and the user adds the ones
// they want (SuggestRulesView, which the Rules panel switches to).
interface ISuggestRulesService
{
    // Whether this host can ask Claude: the web host and VS Code, not the standalone browser app.
    bool IsAvailable { get; }

    // The prompt for any AI; empty when there is no model.
    string GetPrompt();

    Task<Result<RuleSuggestions>> AskAsync(AiSource route, Action<SuggestStep> onStep);

    // Reads an AI chat's answer, pasted back, and checks its rules.
    Result<RuleSuggestions> ParsePasted(string text);

    // Checks each suggested rule on the model as it is now.
    RuleSuggestions Check(RuleSuggestions suggestions);

    // A key that changes whenever a check could give another result (a re-parse, a rule added).
    object CheckVersion { get; }

    // Adds the rules, as one undo step, and tells the user how many were added (not for one rule
    // added as asked: its row shows it).
    void Add(IReadOnlyList<SuggestedRule> chosen);
}

[Scoped]
class SuggestRulesService(
    IModelMgr modelMgr,
    IModelQueries modelQueries,
    IRuleService ruleService,
    IAiService aiService,
    IApplicationEvents applicationEvents
) : ISuggestRulesService
{
    public bool IsAvailable => aiService.IsAvailable;

    public object CheckVersion => modelMgr.WithModel(m => (m.StructureVersion, m.RulesVersion));

    public void Add(IReadOnlyList<SuggestedRule> chosen)
    {
        // Checked names, so two suggestions that name one rule differently add it once; a node
        // gone in a re-parse since the check leaves its rule out.
        List<ArchitectureRule> rules = [.. chosen.Select(r => new ArchitectureRule(r.Check.FromName, r.Check.ToName))];
        var added = ruleService.AddRules(rules);
        var left = rules.Distinct().Count() - added;
        if (chosen.Count == 1 && added == 1)
            return;
        var message = added == 1 ? "Added 1 rule." : $"Added {added} rules.";
        if (left > 0)
            message +=
                left == 1
                    ? " 1 was left out: it is a rule already, or its nodes are no longer in the model."
                    : $" {left} were left out: they are rules already, or their nodes are no longer in the model.";
        applicationEvents.TriggerInfoReported(message);
    }

    public Result<RuleSuggestions> ParsePasted(string text)
    {
        var parsed = RuleSuggestions.ParseText(text);
        return parsed is RuleSuggestions suggestions ? Check(suggestions) : parsed.Error;
    }

    public string GetPrompt()
    {
        using var model = modelMgr.UseModel();
        return SuggestRulesPrompt.Build(model);
    }

    public async Task<Result<RuleSuggestions>> AskAsync(AiSource route, Action<SuggestStep> onStep)
    {
        // The e2e tests exercise the dialog without a key or network.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route is AiSource.ClaudeCode ? SuggestStep.AskingClaudeCode : SuggestStep.AskingClaude);
            await Task.Delay(TestModeAnswerDelay);
            return Check(TestModeAnswer() with { Source = route });
        }

        var json = await aiService.AskAsync(
            route,
            () => Task.FromResult(GetPrompt()),
            RuleSuggestions.JsonSchema,
            onStep,
            withModelTools: true
        );
        if (json is not string text)
            return json.Error;
        var parsed = RuleSuggestions.Parse(text, route);
        return parsed is RuleSuggestions suggestions ? Check(suggestions) : parsed.Error;
    }

    public RuleSuggestions Check(RuleSuggestions suggestions) =>
        suggestions with
        {
            Rules = [.. suggestions.Rules.Select(r => r with { Check = CheckRule(r) })],
        };

    RuleCheck CheckRule(SuggestedRule rule)
    {
        var checkedRule = modelQueries.CheckRule(rule.From, rule.To);
        if (checkedRule is not RuleResult result)
            return new RuleCheck(checkedRule.Error.Message);
        using var model = modelMgr.UseModel();
        return new RuleCheck(
            null,
            result.From,
            result.To,
            Title(model, result.From),
            Title(model, result.To),
            result.ViolationCount,
            result.LinkCount,
            result.IsRule
        );
    }

    static string Title(IModel model, string name) =>
        model.Nodes.TryGetValue(NodeId.FromName(name), out var node) ? node.LongName : name;

    // Two rules between the first two top-level containers, either way, and one on a node that is
    // not there, so the dialog shows a kept or broken rule and one it cannot add.
    RuleSuggestions TestModeAnswer()
    {
        List<SuggestedRule> rules = [];
        using (var model = modelMgr.UseModel())
        {
            var top = SuggestRulesPrompt.ListedNodes(model).Where(n => n.Parent.IsRoot).Take(2).ToList();
            if (top.Count == 2)
            {
                rules.Add(new SuggestedRule(top[0].Name, top[1].Name, "Test mode: Claude was not asked."));
                rules.Add(new SuggestedRule(top[1].Name, top[0].Name, "Test mode: Claude was not asked."));
            }
        }
        rules.Add(new SuggestedRule("No.Such.Node", "Other", "Test mode: a node that is not in the model."));
        return new RuleSuggestions(rules);
    }

    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);
}
