using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dependinator.Core.ModelQueries;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Ai;
using Dependinator.UI.Modeling;
using Dependinator.UI.Modeling.Models;
using MudBlazor;
using static Dependinator.UI.Diagrams.Ai.PromptParts;

namespace Dependinator.UI.Diagrams.Rules;

// A rule an AI suggests, and what Dependinator itself found when it checked the rule on the model
// (the AI's own counts are not trusted): the nodes it names, and what it would break today. Nodes
// are from and to, the layers top first, or the one node, by kind.
record SuggestedRule(RuleKind Kind, IReadOnlyList<string> Nodes, string Why)
{
    public SuggestedRule(string from, string to, string why)
        : this(RuleKind.MustNotUse, [from, to], why) { }

    public RuleCheck Check { get; init; } = RuleCheck.Unchecked;

    public string From => Nodes.Count > 0 ? Nodes[0] : "";
    public string To => Nodes.Count > 0 ? Nodes[^1] : "";

    public bool CanAdd => Check.Error is null && !Check.IsRule && !Check.IsDismissed;

    // The rule as checked (the AI's names where the check found none), the key that dismissals
    // and the suggestions shown before are matched by.
    public ArchitectureRule Key =>
        Check.Rule
        ?? ArchitectureRule.Of(Kind, Nodes)
        ?? new ArchitectureRule(Nodes.FirstOrDefault() ?? "", Nodes.LastOrDefault() ?? "", Kind: Kind);
}

// Rule is the checked rule (node names), Titles its nodes as the panel shows them; Error says why
// the rule cannot be added (a node not found, one inside another). IsDismissed: the user said no to it.
record RuleCheck(
    string? Error,
    ArchitectureRule? Rule = null,
    IReadOnlyList<string>? Titles = null,
    int ViolationCount = 0,
    int LinkCount = 0,
    bool IsRule = false,
    bool IsDismissed = false
)
{
    public static readonly RuleCheck Unchecked = new("Not checked");
}

// An AI's suggested rules, as the Suggest rules dialog shows them.
record RuleSuggestions(IReadOnlyList<SuggestedRule> Rules) : IAiAnswer
{
    public AiSource Source { get; init; } = AiSource.Claude;

    public string Verdict =>
        Rules.Count switch
        {
            0 => "no new rules",
            1 => "1 rule",
            _ => $"{Rules.Count} rules",
        };

    // How many rules the answer had that were left out: dismissed by the user, or suggested in an
    // earlier answer already.
    public int LeftOutCount { get; init; }

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
                  "kind": { "type": "string", "enum": ["must_not_use", "only_may_use", "layers", "independent", "no_cycles", "siblings_independent"], "description": "The kind of rule." },
                  "nodes": { "type": "array", "items": { "type": "string" }, "description": "The node names, as listed: from and to for must_not_use and only_may_use, the layers top first for layers, the one node for independent, no_cycles and siblings_independent." },
                  "why": { "type": "string", "description": "1–2 sentences: what the rule protects." }
                },
                "required": ["kind", "nodes", "why"],
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
        List<SuggestedRule> rules = [];
        foreach (var r in answer.Rules ?? [])
        {
            var nodes = (r.Nodes ?? [r.From ?? "", r.To ?? ""]).Select(n => Name(n ?? "")).ToList();
            // A blank name, an unknown kind or nodes that do not fit the kind are left out; the rest
            // are checked, which says what is wrong with them.
            if (
                nodes.All(n => n != "")
                && ArchitectureRule.KindNames.TryGetValue(r.Kind ?? "must_not_use", out var kind)
                && ArchitectureRule.Of(kind, nodes) is not null
            )
                rules.Add(new SuggestedRule(kind, nodes, (r.Why ?? "").Trim()));
        }
        // No rules is a fine answer: the rules there are may cover the architecture already.
        return new RuleSuggestions(rules) { Source = source };
    }

    record Answer(List<AnswerRule>? Rules);

    // From and To: the answer format before rule kinds (a must-not-use rule).
    record AnswerRule(string? Kind, List<string>? Nodes, string? Why, string? From = null, string? To = null);

    // "Rule: <the rule>", markdown emphasis, list marks and quotes aside; the rule in one of the
    // forms the prompt shows (SuggestRulesPrompt.TaskSection).
    static readonly Regex RuleLine = new(
        @"^[\s>*_#\-\d.)]*Rule\s*\d*[*_]*\s*:[*_]*\s*(?<rule>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Multiline
    );

    static readonly Regex MustNotUse = new(
        @"^(?<a>.+?)\s+must\s+not\s+(?:use|depend\s+on)\s+(?<b>.+)$",
        RegexOptions.IgnoreCase
    );
    static readonly Regex OnlyMayUse = new(@"^only\s+(?<a>.+?)\s+may\s+use\s+(?<b>.+)$", RegexOptions.IgnoreCase);
    static readonly Regex Layers = new(@"^layers\s*:?\s+(?<list>.+)$", RegexOptions.IgnoreCase);
    static readonly Regex SiblingsIndependent = new(
        @"^the\s+parts\s+of\s+(?<a>.+?)\s+are\s+independent\b.*$",
        RegexOptions.IgnoreCase
    );
    static readonly Regex Independent = new(@"^(?<a>.+?)\s+is\s+independent\b.*$", RegexOptions.IgnoreCase);
    static readonly Regex NoCycles = new(@"^no\s+cycles\s+inside\s+(?<a>.+)$", RegexOptions.IgnoreCase);

    static readonly Regex TrailingNote = new(@"\s*\([^()]*\)\s*$");

    static readonly Regex WhyLine = new(@"^[\s>*_\-]*Why[*_]*\s*:[*_]*\s*(?<why>.*)$", RegexOptions.IgnoreCase);

    // The rule of a "Rule:" line, null when it has none of the forms.
    static (RuleKind Kind, List<string> Nodes)? ParseRule(string text)
    {
        // A note after the rule, as the prompt lists layers ("(each may use only the layers below it)").
        text = TrailingNote.Replace(text.Trim().Trim('*', '_').Trim(), "");
        // The two-node forms first, so a why on the same line ("… so UI is independent") is no rule.
        if (OnlyMayUse.Match(text) is { Success: true } only)
            return (RuleKind.OnlyMayUse, [Name(only.Groups["a"].Value), Name(only.Groups["b"].Value)]);
        if (Layers.Match(text) is { Success: true } layers)
            return (
                RuleKind.Layers,
                [.. layers.Groups["list"].Value.Split(['>', '›'], StringSplitOptions.RemoveEmptyEntries).Select(Name)]
            );
        if (MustNotUse.Match(text) is { Success: true } mustNot)
            return (RuleKind.MustNotUse, [Name(mustNot.Groups["a"].Value), Name(mustNot.Groups["b"].Value)]);
        if (NoCycles.Match(text) is { Success: true } noCycles)
            return (RuleKind.NoCycles, [Name(noCycles.Groups["a"].Value)]);
        if (SiblingsIndependent.Match(text) is { Success: true } siblings)
            return (RuleKind.SiblingsIndependent, [Name(siblings.Groups["a"].Value)]);
        if (Independent.Match(text) is { Success: true } independent)
            return (RuleKind.Independent, [Name(independent.Groups["a"].Value)]);
        return null;
    }

    // An AI chat's answer to the copied prompt, pasted back: each "Rule:" line, and the "Why:"
    // that follows it.
    public static Result<RuleSuggestions> ParseText(string text)
    {
        List<SuggestedRule> rules = [];
        var lines = text.Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (
                RuleLine.Match(lines[i]) is not { Success: true } line
                || ParseRule(line.Groups["rule"].Value) is not var (kind, nodes)
                || nodes.Any(n => n == "")
                || ArchitectureRule.Of(kind, nodes) is null
            )
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
            rules.Add(new SuggestedRule(kind, nodes, why));
        }
        return rules.Count == 0
            ? new Error("The answer has no \"Rule: …\" lines, so it could not be read.")
            : new RuleSuggestions(rules) { Source = AiSource.Chat };
    }

    // A name in backticks is the name, whatever follows it ("`Data`, so the UI stays thin").
    static string Name(string text) =>
        BacktickName.Match(text) is { Success: true } quoted
            ? quoted.Groups["name"].Value.Trim()
            : text.Trim().Trim('`', '*', '_', '"', '\'', '.', ',', ';').Trim();

    static readonly Regex BacktickName = new(@"^\s*[*_]*`(?<name>[^`]+)`");
}

// The prompt that asks an AI to suggest architecture rules: the model's structure (containers
// down to a depth that fits), the dependencies between the containers listed deepest, and the
// rules there are. The AI names nodes as listed; Dependinator then checks each rule itself.
static class SuggestRulesPrompt
{
    internal const int MaxSuggestions = 8;
    internal const int MaxListedNodes = 120;
    internal const int MaxDependencies = 150;

    // suggestedBefore: the rules earlier answers suggested (node names), not to be repeated.
    public static string Build(IModel model, IReadOnlyCollection<ArchitectureRule>? suggestedBefore = null)
    {
        var outline = Outline(model);
        if (outline == "")
            return "";

        var text = new StringBuilder();
        text.AppendLine("# Suggest architecture rules").AppendLine();
        text.AppendLine(
            "Dependinator shows a codebase as a dependency model: containers (projects, namespaces, modules), the "
                + "types in them, and the links between them. An architecture rule guards the architecture as the "
                + "code changes: Dependinator counts the dependencies that break each rule and draws them in the "
                + "diagram. The kinds of rule (\"at or inside\" a node includes everything it contains):"
        );
        text.AppendLine()
            .AppendLine("- `A` must not use `B`: nothing in A may depend on anything in B.")
            .AppendLine("- only `A` may use `B`: nothing outside A and B may depend on anything in B.")
            .AppendLine("- layers `A` > `B` > `C`: each layer may use only the layers below it, never one above.")
            .AppendLine("- `A` is independent: nothing in A may depend on the rest of the codebase, only on externals.")
            .AppendLine("- the parts of `A` are independent: no child of A may depend on another child of A.")
            .AppendLine("- no cycles inside `A`: nothing in A depends on itself in a circle.");
        text.AppendLine().Append(outline);
        AppendRules(text, model);
        AppendNotToSuggest(text, model, suggestedBefore);
        text.AppendLine().Append(TaskSection);
        return text.ToString();
    }

    // The model's structure (containers, as many levels as fit) and the dependencies between the
    // containers listed deepest, as sections of a prompt; empty when the model has no containers.
    // The tour prompt (TourPrompt) shares it, without the external references (withExternals),
    // which a tour does not visit and which would take the room of the codebase's own levels.
    internal static string Outline(IModel model, bool withExternals = true)
    {
        var (listed, leftOut) = ListContainers(model, withExternals);
        if (listed.Count == 0)
            return "";
        var typeCounts = TypeCounts(model);

        var text = new StringBuilder();
        text.AppendLine($"## The structure of {DisplayPath(model, model.Path)}").AppendLine();
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
        return text.ToString();
    }

    static readonly string TaskSection = $"""
        ## Your task

        Suggest the rules clearly worth keeping for this codebase, at most {MaxSuggestions}: rules that state its intended layering or boundaries (which part may use which, which part stays free of a technology, which parts stay apart), not rules that merely happen to hold. Pick the kind that says it best: one layers rule rather than several "must not use" rules that say the same, "is independent" for a shared or core part that should use nothing else, "the parts of" for features or modules meant to stay apart. Fewer is better than padding: suggest only rules you would defend, and none when the rules there are already cover the architecture. Prefer rules the code keeps today, or breaks with only a few dependencies; suggest a rule the code breaks widely only when the structure clearly intends it, and say so. Leave out the rules there are already, the rules not to suggest, and rules where one node contains another. Name each node exactly as listed (the name in backticks). If you have Dependinator's tools, check each rule with check_rule before you suggest it.

        Answer in this format, for each rule, with the rule in one of the forms above:

        Rule: <name> must not use <name>
        Why: 1–2 sentences on what the rule protects.

        """;

    // The containers, level by level, as many whole levels as fit (at least the first, cut at the
    // limit), each right after its parent so the indented list reads as a tree.
    internal static List<Node> ListedNodes(IModel model) => ListContainers(model).Listed;

    static (List<Node> Listed, int LeftOut) ListContainers(IModel model, bool withExternals = true)
    {
        HashSet<Node> included = [];
        var level = Containers(model.Root)
            .Where(n => withExternals || n.Type != Dependinator.Core.Parsing.NodeType.Externals)
            .ToList();
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
            text.AppendLine($"- {rule.Describe(Quoted)}{Because(rule.Reason)}");
    }

    // The rules the user dismissed (with why, where they gave one) and those suggested before.
    static void AppendNotToSuggest(
        StringBuilder text,
        IModel model,
        IReadOnlyCollection<ArchitectureRule>? suggestedBefore
    )
    {
        var dismissed = model.DismissedRules;
        var before = (suggestedBefore ?? [])
            .Where(r => !dismissed.Any(d => d.IsFor(r)) && !model.Rules.Contains(r))
            .ToList();
        if (dismissed.Count == 0 && before.Count == 0)
            return;
        text.AppendLine().AppendLine("## Rules not to suggest").AppendLine();
        text.AppendLine("The user said no to these, or saw them suggested already; do not suggest them again.")
            .AppendLine();
        foreach (var d in dismissed)
            text.AppendLine($"- {d.Rule.Describe(Quoted)} (dismissed{Because(d.Reason, ": ")})");
        foreach (var rule in before)
            text.AppendLine($"- {rule.Describe(Quoted)} (suggested before)");
    }

    static string Quoted(string name) => $"`{name}`";

    static string Because(string reason, string separator = " — ") => reason == "" ? "" : $"{separator}{reason}";
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

    // Adds the rules, with the AI's why as their reason, as one undo step, and tells the user how
    // many were added (not for one rule added as asked: its row shows it).
    void Add(IReadOnlyList<SuggestedRule> chosen);

    // Remembers the suggested rule as not wanted: later answers leave it out.
    void Dismiss(SuggestedRule rule);

    // Takes a dismissal back: the rule can be suggested again.
    void Undismiss(SuggestedRule rule);

    // Lets later answers suggest the rule again, though an earlier answer suggested it (its
    // dismissal was taken back in the Rules panel).
    void AllowSuggestingAgain(ArchitectureRule rule);
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
        List<ArchitectureRule> rules =
        [
            .. chosen.Where(r => r.Check.Rule is not null).Select(r => r.Check.Rule! with { Reason = r.Why }),
        ];
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

    public void Dismiss(SuggestedRule rule) => ruleService.Dismiss(rule.Key, "");

    public void Undismiss(SuggestedRule rule)
    {
        ruleService.Undismiss(DismissedRule.Of(rule.Key));
        AllowSuggestingAgain(rule.Key);
    }

    public void AllowSuggestingAgain(ArchitectureRule rule) => suggestedBefore.Remove(rule with { Reason = "" });

    public Result<RuleSuggestions> ParsePasted(string text)
    {
        var parsed = RuleSuggestions.ParseText(text);
        return parsed is RuleSuggestions suggestions ? New(Check(suggestions)) : parsed.Error;
    }

    public string GetPrompt()
    {
        using var model = modelMgr.UseModel();
        return SuggestRulesPrompt.Build(model, SuggestedBefore(model));
    }

    // The rules earlier answers suggested for the open model (node names as checked), so a new
    // ask is told not to repeat them and its repeats are left out; forgotten for another model.
    readonly HashSet<ArchitectureRule> suggestedBefore = [];
    string suggestedFor = "";

    IReadOnlyCollection<ArchitectureRule> SuggestedBefore(IModel model)
    {
        if (model.Path != suggestedFor)
        {
            suggestedBefore.Clear();
            suggestedFor = model.Path;
        }
        return suggestedBefore;
    }

    // A checked answer without the rules the user dismissed and those an earlier answer
    // suggested, each named once; the rest are remembered as suggested.
    RuleSuggestions New(RuleSuggestions answer)
    {
        using var model = modelMgr.UseModel();
        var before = SuggestedBefore(model);
        HashSet<ArchitectureRule> seen = [];
        List<SuggestedRule> kept = [];
        var leftOut = 0;
        foreach (var rule in answer.Rules)
        {
            if (rule.Check.IsDismissed || before.Contains(rule.Key))
                leftOut++;
            else if (seen.Add(rule.Key))
                kept.Add(rule); // A rule named twice in one answer is shown once, not counted as left out.
        }
        foreach (var rule in kept.Where(r => r.Check.Error is null))
            suggestedBefore.Add(rule.Key);
        return answer with { Rules = kept, LeftOutCount = leftOut };
    }

    public async Task<Result<RuleSuggestions>> AskAsync(AiSource route, Action<SuggestStep> onStep)
    {
        // The e2e tests exercise the dialog without a key or network.
        if (Dependinator.Core.Build.IsTestMode)
        {
            onStep(route.AskingStep());
            await Task.Delay(TestModeAnswerDelay);
            return New(Check(TestModeAnswer() with { Source = route }));
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
        return parsed is RuleSuggestions suggestions ? New(Check(suggestions)) : parsed.Error;
    }

    public RuleSuggestions Check(RuleSuggestions suggestions) =>
        suggestions with
        {
            Rules = [.. suggestions.Rules.Select(r => r with { Check = CheckRule(r) })],
        };

    RuleCheck CheckRule(SuggestedRule rule)
    {
        var checkedRule = modelQueries.CheckRule(ArchitectureRule.KindName(rule.Kind), rule.Nodes);
        if (checkedRule is not RuleResult result)
            return new RuleCheck(checkedRule.Error.Message);
        if (ArchitectureRule.Of(rule.Kind, result.Nodes ?? []) is not { } found)
            return new RuleCheck("The rule could not be checked.");
        using var model = modelMgr.UseModel();
        return new RuleCheck(
            null,
            found,
            [.. found.NodeNames.Select(n => Title(model, n))],
            result.ViolationCount,
            result.LinkCount,
            result.IsRule,
            model.DismissedRules.Any(d => d.IsFor(found))
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
        if (rules.Count == 3)
            rules.Add(new SuggestedRule(RuleKind.SiblingsIndependent, [rules[0].To], "Test mode: another kind."));
        return new RuleSuggestions(rules);
    }

    static readonly TimeSpan TestModeAnswerDelay = TimeSpan.FromMilliseconds(800);
}
